using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Application.TechnicalHierarchy;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Api.Security;

public sealed class ImportPathFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var root = configuration["Imports:StoragePath"] ?? "data/imports";
        string Resolve(string path)
        {
            var full = StoragePathPolicy.Resolve(root, path, allowAbsolute: true);
            if (!File.Exists(full)) throw new DomainException("No se encontro el archivo de importacion.");
            if (new FileInfo(full).Length > UploadPolicy.MaximumBytes) throw new DomainException("La importacion supera 20 MB.");
            UploadPolicy.Validate(Path.GetFileName(full), UploadPolicy.ExcelMime, File.ReadAllBytes(full));
            return full;
        }
        for (var i = 0; i < context.Arguments.Count; i++)
        {
            context.Arguments[i] = context.Arguments[i] switch
            {
                AlertsExcelImportRequest request => request with { PdfTemplatesPath = Resolve(request.PdfTemplatesPath), AlertRulesPath = Resolve(request.AlertRulesPath), AlertsPath = Resolve(request.AlertsPath), NotificationsPath = Resolve(request.NotificationsPath) },
                FileMetadataExcelImportRequest request => request with { ExcelPath = Resolve(request.ExcelPath) },
                TechnicalHierarchyExcelImportRequest request => request with { SistemasComponentesPath = Resolve(request.SistemasComponentesPath), UbicacionesTecnicasPath = Resolve(request.UbicacionesTecnicasPath) },
                _ => context.Arguments[i]
            };
        }
        return await next(context);
    }
}
