using System.Text.Json;
using ClosedXML.Excel;
using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Imports;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Imports;

public sealed class ExcelImportWorkflowService : IExcelImportWorkflowService
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IDataProvider _dataProvider;
    private readonly IExcelSchemaRegistry _schemaRegistry;
    private readonly IAuditService _auditService;
    private readonly IDocumentStorageService _documentStorageService;
    private readonly string _basePath;

    public ExcelImportWorkflowService(
        IDataProvider dataProvider,
        IExcelSchemaRegistry schemaRegistry,
        IAuditService auditService,
        IDocumentStorageService documentStorageService,
        IOptions<ImportStorageOptions> options)
    {
        _dataProvider = dataProvider;
        _schemaRegistry = schemaRegistry;
        _auditService = auditService;
        _documentStorageService = documentStorageService;
        _basePath = ExcelPathResolver.Resolve(options.Value.StoragePath);
    }

    public async Task<ExcelImportPreviewResult> UploadAsync(
        ExcelImportUploadCommand command,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(command.Entity, nameof(command.Entity));
        DomainGuard.AgainstEmpty(command.OriginalFileName, nameof(command.OriginalFileName));
        DomainGuard.AgainstEmpty(command.UploadedBy, nameof(command.UploadedBy));

        var schema = ResolveImportSchema(command.Entity);
        var workbook = ReadWorkbook(command.Content, schema);
        var preview = await BuildPreviewAsync(schema, workbook, cancellationToken);
        var status = preview.Errors.Count == 0 && !command.SimulateOnly
            ? ImportStatus.PendingApproval
            : ImportStatus.Validating;

        var id = Guid.NewGuid().ToString("D");
        EnsureStorage();

        var safeFileName = $"{id}_{Path.GetFileName(command.OriginalFileName)}";
        var originalPath = Path.Combine(OriginalsPath, safeFileName);
        await File.WriteAllBytesAsync(originalPath, command.Content, cancellationToken);
        await _documentStorageService.SaveImportBackupAsync(new DocumentStorageSaveRequest(
            "Imports",
            "ExcelImport",
            id,
            safeFileName,
            ContentType,
            command.Content,
            command.UploadedBy,
            DocumentStoragePurpose.ImportBackup,
            Metadata: new Dictionary<string, string?>
            {
                ["SchemaName"] = schema.SchemaName,
                ["Entity"] = command.Entity,
                ["OriginalFileName"] = command.OriginalFileName,
                ["SimulateOnly"] = command.SimulateOnly.ToString()
            }), cancellationToken);

        var record = new StoredExcelImport(
            id,
            command.Entity.Trim(),
            schema.SchemaName,
            Path.GetFileName(command.OriginalFileName),
            safeFileName,
            command.SimulateOnly,
            status,
            DateTimeOffset.UtcNow,
            command.UploadedBy,
            null,
            null,
            null,
            null,
            null,
            preview.Summary,
            preview.Rows,
            preview.Errors);

        await SaveRecordAsync(record, cancellationToken);

        await _auditService.RecordAsync(new AuditEventRequest(
            command.UploadedBy,
            "import.uploaded",
            AuditModules.Imports,
            "ExcelImport",
            id,
            NewValue: schema.SchemaName,
            Severity: preview.Errors.Count == 0 ? AuditSeverity.Medium : AuditSeverity.High,
            Detail: $"Archivo {command.OriginalFileName} cargado para {schema.SchemaName}. Simulacion: {command.SimulateOnly}"), cancellationToken);

        return ToPreviewResult(record);
    }

    public async Task<IReadOnlyCollection<ExcelImportListItem>> ListAsync(CancellationToken cancellationToken)
    {
        EnsureStorage();
        var records = new List<StoredExcelImport>();

        foreach (var path in Directory.EnumerateFiles(MetadataPath, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(await ReadRecordAsync(path, cancellationToken));
        }

        return records
            .OrderByDescending(record => record.UploadedAtUtc)
            .Select(ToListItem)
            .ToArray();
    }

    public async Task<ExcelImportPreviewResult?> GetPreviewAsync(string id, CancellationToken cancellationToken)
    {
        var record = await FindRecordAsync(id, cancellationToken);
        return record is null ? null : ToPreviewResult(record);
    }

    public async Task<ExcelImportPreviewResult?> ApproveAsync(string id, string approvedBy, CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(approvedBy, nameof(approvedBy));
        var record = await FindRecordAsync(id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (record.SimulateOnly)
        {
            throw new DomainException("Las simulaciones no se pueden aprobar ni aplicar al maestro oficial.");
        }

        if (record.Status is ImportStatus.Applied or ImportStatus.Rejected)
        {
            throw new DomainException("La importacion ya fue cerrada.");
        }

        var schema = _schemaRegistry.GetRequired(record.SchemaName);
        record = await RefreshPreviewWithCurrentSchemaAsync(record, schema, cancellationToken);
        if (record.Errors.Count > 0)
        {
            throw new DomainException("No se puede aprobar una importacion con errores.");
        }

        try
        {
            var officialRows = (await _dataProvider.ReadRowsAsync(schema.SchemaName, cancellationToken)).ToList();
            var officialByKey = officialRows
                .Where(row => !string.IsNullOrWhiteSpace(BuildNaturalKey(schema, row).Replace("|", string.Empty)))
                .ToDictionary(row => BuildNaturalKey(schema, row), row => row, StringComparer.OrdinalIgnoreCase);

            foreach (var previewRow in record.Rows)
            {
                var dataRow = new DataRow(previewRow.Values);
                officialByKey[BuildNaturalKey(schema, dataRow)] = NormalizeRow(schema, dataRow);
            }

            await _dataProvider.SaveRowsAsync(schema.SchemaName, officialByKey.Values.ToArray(), cancellationToken);
        }
        catch (DomainException ex)
        {
            record = record with
            {
                Status = ImportStatus.Failed,
                RejectReason = ex.Message
            };
            await SaveRecordAsync(record, cancellationToken);

            await _auditService.RecordAsync(new AuditEventRequest(
                approvedBy,
                "import.failed",
                AuditModules.Imports,
                "ExcelImport",
                record.Id,
                PreviousValue: record.SchemaName,
                Severity: AuditSeverity.Critical,
                Reason: ex.Message,
                Detail: $"No se pudo aplicar la importacion: {record.OriginalFileName}"), cancellationToken);

            throw;
        }

        record = record with
        {
            Status = ImportStatus.Applied,
            AppliedAtUtc = DateTimeOffset.UtcNow,
            AppliedBy = approvedBy
        };
        await SaveRecordAsync(record, cancellationToken);

        await _auditService.RecordAsync(new AuditEventRequest(
            approvedBy,
            "import.applied",
            AuditModules.Imports,
            "ExcelImport",
            record.Id,
            NewValue: record.SchemaName,
            Severity: AuditSeverity.Critical,
            Reason: "Aprobacion de importacion Excel",
            Detail: $"Importacion aplicada al maestro oficial: {record.OriginalFileName}"), cancellationToken);

        return ToPreviewResult(record);
    }

    public async Task<ExcelImportPreviewResult?> RejectAsync(
        string id,
        string rejectedBy,
        string? reason,
        CancellationToken cancellationToken)
    {
        DomainGuard.AgainstEmpty(rejectedBy, nameof(rejectedBy));
        var record = await FindRecordAsync(id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (record.Status is ImportStatus.Applied)
        {
            throw new DomainException("No se puede rechazar una importacion ya aplicada.");
        }

        record = record with
        {
            Status = ImportStatus.Rejected,
            RejectedAtUtc = DateTimeOffset.UtcNow,
            RejectedBy = rejectedBy,
            RejectReason = reason
        };
        await SaveRecordAsync(record, cancellationToken);

        await _auditService.RecordAsync(new AuditEventRequest(
            rejectedBy,
            "import.rejected",
            AuditModules.Imports,
            "ExcelImport",
            record.Id,
            PreviousValue: record.SchemaName,
            Severity: AuditSeverity.High,
            Reason: reason,
            Detail: $"Importacion rechazada: {record.OriginalFileName}"), cancellationToken);

        return ToPreviewResult(record);
    }

    public Task<ExcelImportTemplate> CreateTemplateAsync(string entity, CancellationToken cancellationToken)
    {
        var schema = ResolveImportSchema(entity);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(schema.WorksheetName);

        var columnIndex = 1;
        foreach (var column in schema.Columns)
        {
            var cell = worksheet.Cell(1, columnIndex);
            cell.Value = column.Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = column.IsRequired ? XLColor.LightYellow : XLColor.White;
            worksheet.Cell(2, columnIndex).Value = column.Type.ToString();
            columnIndex++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"plantilla_{schema.SchemaName}.xlsx";
        return Task.FromResult(new ExcelImportTemplate(fileName, ContentType, stream.ToArray()));
    }

    private async Task<ImportPreviewData> BuildPreviewAsync(
        ExcelFileSchema schema,
        ParsedWorkbook workbook,
        CancellationToken cancellationToken)
    {
        var errors = new List<ExcelImportValidationError>();
        var missingColumns = schema.Columns
            .Where(column => column.IsRequired && !workbook.Headers.Contains(column.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        errors.AddRange(missingColumns.Select(column =>
            new ExcelImportValidationError(1, column.Name, $"La columna requerida '{column.Name}' no existe.")));

        var rowValidation = ExcelRowValidator.Validate(schema, workbook.Rows)
            .Select(error => new ExcelImportValidationError(error.RowNumber, error.ColumnName, error.Message))
            .ToArray();
        errors.AddRange(rowValidation);

        errors.AddRange(await ValidateReferencesAsync(schema, workbook.Rows, cancellationToken));

        var errorsByRow = errors
            .GroupBy(error => error.RowNumber)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<ExcelImportValidationError>)group.ToArray());

        var existingRows = await _dataProvider.ReadRowsAsync(schema.SchemaName, cancellationToken);
        var existingByKey = existingRows
            .Where(row => !string.IsNullOrWhiteSpace(BuildNaturalKey(schema, row)))
            .ToDictionary(row => BuildNaturalKey(schema, row), row => row, StringComparer.OrdinalIgnoreCase);

        var duplicateRowNumbers = workbook.Rows
            .Select((row, index) => new
            {
                RowNumber = index + 2,
                Key = BuildNaturalKey(schema, row)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.RowNumber))
            .ToHashSet();

        var previewRows = workbook.Rows
            .Select((row, index) =>
            {
                var rowNumber = index + 2;
                var key = BuildNaturalKey(schema, row);
                var rowErrors = errorsByRow.TryGetValue(rowNumber, out var foundErrors)
                    ? foundErrors
                    : Array.Empty<ExcelImportValidationError>();

                return new ExcelImportPreviewRow(
                    rowNumber,
                    NormalizeValues(schema, row),
                    ResolveOperation(schema, row, key, rowErrors, existingByKey),
                    rowErrors);
            })
            .ToArray();

        var summary = new ImportPreviewSummary(
            previewRows.Length,
            previewRows.Count(row => row.Operation == "Nuevo"),
            previewRows.Count(row => row.Operation == "Actualizado"),
            previewRows.Count(row => row.Operation == "SinCambios"),
            previewRows.Count(row => row.Errors.Count > 0),
            duplicateRowNumbers.Count);

        return new ImportPreviewData(summary, previewRows, errors);
    }

    private async Task<IReadOnlyCollection<ExcelImportValidationError>> ValidateReferencesAsync(
        ExcelFileSchema schema,
        IReadOnlyCollection<DataRow> rows,
        CancellationToken cancellationToken)
    {
        var errors = new List<ExcelImportValidationError>();
        var masterCodes = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        async Task<IReadOnlySet<string>> LoadCodesAsync(string schemaName, string columnName)
        {
            var key = $"{schemaName}:{columnName}";
            if (masterCodes.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var values = (await _dataProvider.ReadRowsAsync(schemaName, cancellationToken))
                .Select(row => row.GetValue(columnName)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            masterCodes[key] = values;
            return values;
        }

        var existingAndIncomingUbicaciones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schema.SchemaName == "ubicaciones_tecnicas")
        {
            foreach (var row in rows)
            {
                var code = row.GetValue("Codigo");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    existingAndIncomingUbicaciones.Add(code.Trim());
                }
            }
        }

        var existingAndIncomingTechnicalNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schema.SchemaName == "sistemas_componentes")
        {
            var master = await LoadCodesAsync("sistemas_componentes", "Codigo");
            existingAndIncomingTechnicalNodes.UnionWith(master);
            foreach (var row in rows)
            {
                var code = row.GetValue("Codigo");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    existingAndIncomingTechnicalNodes.Add(code.Trim());
                }
            }
        }

        var rowNumber = 2;
        foreach (var row in rows)
        {
            if (schema.SchemaName != "faenas")
            {
                await ValidateCodeAsync(row, rowNumber, "FaenaCodigo", "faenas", "Codigo");
            }

            if (schema.SchemaName != "bodegas")
            {
                await ValidateCodeAsync(row, rowNumber, "BodegaCodigo", "bodegas", "Codigo");
            }

            if (schema.SchemaName != "ubicaciones_tecnicas")
            {
                await ValidateCodeAsync(row, rowNumber, "UbicacionTecnicaCodigo", "ubicaciones_tecnicas", "Codigo");
            }

            if (schema.SchemaName == "ubicaciones_tecnicas")
            {
                var parentCode = row.GetValue("CodigoPadre");
                if (!string.IsNullOrWhiteSpace(parentCode))
                {
                    var master = await LoadCodesAsync("ubicaciones_tecnicas", "Codigo");
                    if (!master.Contains(parentCode.Trim()) && !existingAndIncomingUbicaciones.Contains(parentCode.Trim()))
                    {
                        errors.Add(new ExcelImportValidationError(rowNumber, "CodigoPadre", $"El codigo padre '{parentCode}' no existe en ubicaciones tecnicas."));
                    }
                }
            }

            if (schema.SchemaName == "sistemas_componentes")
            {
                var code = row.GetValue("Codigo")?.Trim();
                var level = row.GetValue("Nivel")?.Trim();
                var parentCode = row.GetValue("CodigoPadre")?.Trim();

                if (!Enum.TryParse<TechnicalHierarchyLevelName>(level, ignoreCase: true, out var parsedLevel))
                {
                    errors.Add(new ExcelImportValidationError(rowNumber, "Nivel", $"El nivel '{level}' no es valido."));
                }
                else if (parsedLevel == TechnicalHierarchyLevelName.Sistema)
                {
                    if (!string.IsNullOrWhiteSpace(parentCode))
                    {
                        errors.Add(new ExcelImportValidationError(rowNumber, "CodigoPadre", "Un sistema no debe tener codigo padre."));
                    }
                }
                else if (string.IsNullOrWhiteSpace(parentCode))
                {
                    errors.Add(new ExcelImportValidationError(rowNumber, "CodigoPadre", $"El nivel '{parsedLevel}' requiere codigo padre."));
                }
                else if (!existingAndIncomingTechnicalNodes.Contains(parentCode))
                {
                    errors.Add(new ExcelImportValidationError(rowNumber, "CodigoPadre", $"El nodo padre '{parentCode}' no existe en sistemas/componentes."));
                }
                else if (!string.IsNullOrWhiteSpace(code) && parentCode.Equals(code, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ExcelImportValidationError(rowNumber, "CodigoPadre", "Un nodo tecnico no puede ser padre de si mismo."));
                }
            }

            await ValidateCodeAsync(row, rowNumber, "RepuestoCodigo", "repuestos", "Codigo");
            await ValidateCodeAsync(row, rowNumber, "ActivoCodigo", "activos", "Codigo");
            if (schema.SchemaName != "disponibilidad_contratos")
            {
                await ValidateCodeAsync(row, rowNumber, "ContractCode", "disponibilidad_contratos", "ContractCode");
            }

            var family = row.GetValue("Familia");
            if (!string.IsNullOrWhiteSpace(family))
            {
                var families = await LoadCodesAsync("repuestos", "Familia");
                if (families.Count > 0 && !families.Contains(family.Trim()))
                {
                    errors.Add(new ExcelImportValidationError(rowNumber, "Familia", $"La familia '{family}' no existe en el maestro de familias."));
                }
            }

            if (schema.SchemaName == "documentos")
            {
                var entityType = row.GetValue("EntidadTipo")?.Trim();
                var entityCode = row.GetValue("EntidadCodigo")?.Trim();
                var documentType = row.GetValue("TipoDocumento")?.Trim();

                if (!string.IsNullOrWhiteSpace(documentType))
                {
                    var documentTypes = await LoadCodesAsync("document_types", "Codigo");
                    if (documentTypes.Count > 0 && !documentTypes.Contains(documentType))
                    {
                        errors.Add(new ExcelImportValidationError(rowNumber, "TipoDocumento", $"El tipo documental '{documentType}' no existe."));
                    }
                }

                if (!string.IsNullOrWhiteSpace(entityType) && !IsValidDocumentEntityType(entityType))
                {
                    errors.Add(new ExcelImportValidationError(rowNumber, "EntidadTipo", $"El tipo de entidad documental '{entityType}' no es valido."));
                }
                else if (!string.IsNullOrWhiteSpace(entityCode) && entityType?.Equals("Activo", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var assets = await LoadCodesAsync("activos", "Codigo");
                    if (!assets.Contains(entityCode))
                    {
                        errors.Add(new ExcelImportValidationError(rowNumber, "EntidadCodigo", $"El activo '{entityCode}' no existe."));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(entityCode) && entityType?.Equals("OT", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var workOrders = await LoadCodesAsync("ordenes_trabajo", "NumeroOT");
                    if (!workOrders.Contains(entityCode))
                    {
                        errors.Add(new ExcelImportValidationError(rowNumber, "EntidadCodigo", $"La OT '{entityCode}' no existe."));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(entityCode) && entityType?.Equals("Faena", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var faenas = await LoadCodesAsync("faenas", "Codigo");
                    if (!faenas.Contains(entityCode))
                    {
                        errors.Add(new ExcelImportValidationError(rowNumber, "EntidadCodigo", $"La faena '{entityCode}' no existe."));
                    }
                }
            }

            rowNumber++;
        }

        return errors;

        async Task ValidateCodeAsync(
            DataRow row,
            int currentRowNumber,
            string sourceColumn,
            string masterSchema,
            string masterColumn)
        {
            var value = row.GetValue(sourceColumn);
            if (string.IsNullOrWhiteSpace(value) || !schema.Columns.Any(column => column.Name.Equals(sourceColumn, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var master = await LoadCodesAsync(masterSchema, masterColumn);
            if (!master.Contains(value.Trim()))
            {
                errors.Add(new ExcelImportValidationError(currentRowNumber, sourceColumn, $"El valor '{value}' no existe en el maestro '{masterSchema}'."));
            }
        }
    }

    private ExcelFileSchema ResolveImportSchema(string entity)
    {
        var normalized = entity.Trim().ToLowerInvariant();
        var schemaName = normalized switch
        {
            "activos" => "activos",
            "faenas" => "faenas",
            "ubicaciones" or "ubicaciones_tecnicas" => "ubicaciones_tecnicas",
            "usuarios" => "usuarios",
            "bodegas" or "almacenes" => "bodegas",
            "repuestos" => "repuestos",
            "stock" or "stock_bodegas" or "stock_por_almacen" => "stock_bodegas",
            "documentos" => "documentos",
            "document_types" or "tipos_documento" or "tipos_documentales" => "document_types",
            "proveedores" => "proveedores",
            "sistemas" or "subsistemas" or "componentes" or "subcomponentes" or "sistemas_componentes" => "sistemas_componentes",
            "planes_preventivos" or "preventivos" => "planes_preventivos",
            "checklists" => "checklists",
            "ot_historicas" or "ordenes_trabajo" or "ot" => "ordenes_trabajo",
            _ => normalized
        };

        var schema = _schemaRegistry.GetRequired(schemaName);
        if (schema.SchemaName is "roles" or "audit_log")
        {
            throw new DomainException($"El esquema '{schema.SchemaName}' no esta habilitado para importacion manual.");
        }

        return schema;
    }

    private static bool IsValidDocumentEntityType(string value)
    {
        return value.Equals("Activo", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("OT", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Faena", StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedWorkbook ReadWorkbook(byte[] content, ExcelFileSchema schema)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(sheet =>
                sheet.Name.Equals(schema.WorksheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var name = worksheet.Cell(1, column).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name) && !headers.ContainsKey(name))
            {
                headers[name] = column;
            }
        }

        var rows = new List<DataRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;

            foreach (var header in headers)
            {
                var value = worksheet.Cell(rowIndex, header.Value).GetFormattedString().Trim();
                values[header.Key] = value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasValue = true;
                }
            }

            foreach (var column in schema.Columns)
            {
                values.TryAdd(column.Name, null);
            }

            if (hasValue)
            {
                rows.Add(new DataRow(values));
            }
        }

        return new ParsedWorkbook(headers.Keys.ToArray(), rows);
    }

    private static string ResolveOperation(
        ExcelFileSchema schema,
        DataRow row,
        string key,
        IReadOnlyCollection<ExcelImportValidationError> errors,
        IReadOnlyDictionary<string, DataRow> existingByKey)
    {
        if (errors.Count > 0)
        {
            return "Error";
        }

        if (!existingByKey.TryGetValue(key, out var existing))
        {
            return "Nuevo";
        }

        return RowsAreEqual(schema, row, existing) ? "SinCambios" : "Actualizado";
    }

    private static bool RowsAreEqual(ExcelFileSchema schema, DataRow left, DataRow right)
    {
        return schema.Columns.All(column =>
            string.Equals(
                left.GetValue(column.Name)?.Trim() ?? string.Empty,
                right.GetValue(column.Name)?.Trim() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildNaturalKey(ExcelFileSchema schema, DataRow row)
    {
        return string.Join("|", schema.NaturalKey.Select(key => row.GetValue(key)?.Trim().ToUpperInvariant() ?? string.Empty));
    }

    private static IReadOnlyDictionary<string, string?> NormalizeValues(ExcelFileSchema schema, DataRow row)
    {
        return schema.Columns.ToDictionary(column => column.Name, column => row.GetValue(column.Name), StringComparer.OrdinalIgnoreCase);
    }

    private static DataRow NormalizeRow(ExcelFileSchema schema, DataRow row)
    {
        return new DataRow(NormalizeValues(schema, row));
    }

    private async Task<StoredExcelImport> RefreshPreviewWithCurrentSchemaAsync(
        StoredExcelImport record,
        ExcelFileSchema schema,
        CancellationToken cancellationToken)
    {
        var originalPath = Path.Combine(OriginalsPath, record.StoredOriginalFileName);
        if (!File.Exists(originalPath))
        {
            return record;
        }

        var content = await File.ReadAllBytesAsync(originalPath, cancellationToken);
        var preview = await BuildPreviewAsync(schema, ReadWorkbook(content, schema), cancellationToken);
        var refreshed = record with
        {
            Status = preview.Errors.Count == 0 ? ImportStatus.PendingApproval : ImportStatus.Validating,
            Summary = preview.Summary,
            Rows = preview.Rows,
            Errors = preview.Errors,
            RejectReason = null
        };

        await SaveRecordAsync(refreshed, cancellationToken);
        return refreshed;
    }

    private async Task<StoredExcelImport?> FindRecordAsync(string id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(MetadataPath, $"{id}.json");
        return File.Exists(path) ? await ReadRecordAsync(path, cancellationToken) : null;
    }

    private async Task<StoredExcelImport> ReadRecordAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<StoredExcelImport>(stream, JsonOptions, cancellationToken)
               ?? throw new DomainException($"No se pudo leer la importacion '{path}'.");
    }

    private async Task SaveRecordAsync(StoredExcelImport record, CancellationToken cancellationToken)
    {
        EnsureStorage();
        var path = Path.Combine(MetadataPath, $"{record.Id}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken);
    }

    private void EnsureStorage()
    {
        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(OriginalsPath);
        Directory.CreateDirectory(MetadataPath);
    }

    private string OriginalsPath => Path.Combine(_basePath, "originals");

    private string MetadataPath => Path.Combine(_basePath, "metadata");

    private static ExcelImportPreviewResult ToPreviewResult(StoredExcelImport record)
    {
        return new ExcelImportPreviewResult(ToListItem(record), record.Rows, record.Errors);
    }

    private static ExcelImportListItem ToListItem(StoredExcelImport record)
    {
        return new ExcelImportListItem(
            record.Id,
            record.Entity,
            record.SchemaName,
            record.OriginalFileName,
            record.Status,
            record.SimulateOnly,
            record.UploadedAtUtc,
            record.UploadedBy,
            record.AppliedAtUtc,
            record.AppliedBy,
            record.RejectedAtUtc,
            record.RejectedBy,
            record.RejectReason,
            record.Summary);
    }

    private sealed record ParsedWorkbook(
        IReadOnlyCollection<string> Headers,
        IReadOnlyCollection<DataRow> Rows);

    private sealed record ImportPreviewData(
        ImportPreviewSummary Summary,
        IReadOnlyCollection<ExcelImportPreviewRow> Rows,
        IReadOnlyCollection<ExcelImportValidationError> Errors);

    private sealed record StoredExcelImport(
        string Id,
        string Entity,
        string SchemaName,
        string OriginalFileName,
        string StoredOriginalFileName,
        bool SimulateOnly,
        ImportStatus Status,
        DateTimeOffset UploadedAtUtc,
        string UploadedBy,
        DateTimeOffset? AppliedAtUtc,
        string? AppliedBy,
        DateTimeOffset? RejectedAtUtc,
        string? RejectedBy,
        string? RejectReason,
        ImportPreviewSummary Summary,
        IReadOnlyCollection<ExcelImportPreviewRow> Rows,
        IReadOnlyCollection<ExcelImportValidationError> Errors);

    private enum TechnicalHierarchyLevelName
    {
        Sistema = 0,
        Subsistema = 1,
        Componente = 2,
        Subcomponente = 3
    }
}
