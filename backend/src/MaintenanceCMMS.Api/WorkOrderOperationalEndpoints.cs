using System.Security.Claims;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Storage;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace MaintenanceCMMS.Api;

internal static class WorkOrderOperationalEndpoints
{
    public static void MapOperationalWorkOrderEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/",async(CreateWorkOrderRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>
        {
            try
            {
                var created=await s.CreateAsync(r,UserAccessContext.FromClaims(p),ct);
                return Results.Created($"/api/work-orders/{created.Summary.NumeroOT}",created);
            }
            catch(Exception ex){return Error(ex);}
        });
        api.MapPut("/{numeroOt}/supervisor",async(string numeroOt,AssignWorkOrderSupervisorRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.AssignSupervisorAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/send-to-supervisor",async(string numeroOt,WorkOrderActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.SendToSupervisorAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/supervisor-assigned",async(ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await Many(async()=>await s.ListSupervisorAssignedAsync(UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/my-assigned",async(ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await Many(async()=>await s.ListMyAssignedAsync(UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/lookups/supervisors",async(string? faenaCodigo,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await Many(async()=>await s.ListSupervisorsAsync(faenaCodigo,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/lookups/technicians",async(string? faenaCodigo,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await Many(async()=>await s.ListTechnicianLookupsAsync(faenaCodigo,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/{numeroOt}/technicians",async(string numeroOt,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ListTechniciansAsync(numeroOt,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/technicians",async(string numeroOt,AssignWorkOrderTechniciansRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.AssignTechniciansAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapDelete("/{numeroOt}/technicians/{usuarioId:guid}",async(string numeroOt,Guid usuarioId,[FromBody] UnassignWorkOrderTechnicianRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.UnassignTechnicianAsync(numeroOt,usuarioId,r,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/{numeroOt}/tasks",async(string numeroOt,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ListTasksAsync(numeroOt,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks",async(string numeroOt,CreateWorkOrderTaskRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.AddTaskAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPut("/{numeroOt}/tasks/{codigoTarea}",async(string numeroOt,string codigoTarea,UpdateWorkOrderTaskRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.UpdateTaskAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapDelete("/{numeroOt}/tasks/{codigoTarea}",async(string numeroOt,string codigoTarea,[FromBody] WorkOrderTaskActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.CancelTaskAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks/{codigoTarea}/start",async(string numeroOt,string codigoTarea,[FromBody] WorkOrderTaskActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.StartTaskAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks/{codigoTarea}/complete",async(string numeroOt,string codigoTarea,[FromBody] WorkOrderTaskActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.CompleteTaskAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks/{codigoTarea}/observe",async(string numeroOt,string codigoTarea,ObserveWorkOrderTaskRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ObserveTaskAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks/{codigoTarea}/approve",async(string numeroOt,string codigoTarea,[FromBody] WorkOrderTaskActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ApproveTaskAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/{numeroOt}/tasks/{codigoTarea}/labor",async(string numeroOt,string codigoTarea,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ListTaskLaborAsync(numeroOt,codigoTarea,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks/{codigoTarea}/labor",async(string numeroOt,string codigoTarea,RegisterOwnLaborRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.RegisterOwnLaborAsync(numeroOt,codigoTarea,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPut("/{numeroOt}/labor/{registroId:guid}",async(string numeroOt,Guid registroId,UpdateOwnLaborRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.UpdateOwnLaborAsync(numeroOt,registroId,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/labor/{registroId:guid}/approve",async(string numeroOt,Guid registroId,WorkOrderTaskActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ApproveLaborAsync(numeroOt,registroId,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/labor/{registroId:guid}/void",async(string numeroOt,Guid registroId,VoidWorkOrderLaborRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.VoidLaborAsync(numeroOt,registroId,r,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/{numeroOt}/tasks/{codigoTarea}/evidences",async(string numeroOt,string codigoTarea,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ListTaskEvidencesAsync(numeroOt,codigoTarea,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/tasks/{codigoTarea}/evidences",UploadEvidenceAsync).Accepts<IFormFile>("multipart/form-data");
        api.MapPost("/{numeroOt}/evidences/{evidenciaId:guid}/void",async(string numeroOt,Guid evidenciaId,VoidWorkOrderEvidenceRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.VoidEvidenceAsync(numeroOt,evidenciaId,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/checklist",async(string numeroOt,AddWorkOrderChecklistItemRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.AddChecklistItemAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPut("/{numeroOt}/checklist/{itemId:guid}",async(string numeroOt,Guid itemId,UpdateChecklistItemRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.UpdateChecklistItemAsync(numeroOt,itemId.ToString("D"),r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/checklist/apply-template",async(string numeroOt,ApplyChecklistTemplateRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await Many(async()=>await s.ApplyChecklistTemplateAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapGet("/{numeroOt}/signatures",async(string numeroOt,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ListSignaturesAsync(numeroOt,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/signatures",UploadSignatureAsync).Accepts<IFormFile>("multipart/form-data");
        api.MapPost("/{numeroOt}/technical-close",async(string numeroOt,WorkOrderActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.CloseTechnicallyAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
        api.MapPost("/{numeroOt}/planning-validation",async(string numeroOt,WorkOrderActionRequest r,ClaimsPrincipal p,IWorkOrderService s,CancellationToken ct)=>await One(async()=>await s.ValidatePlanningAsync(numeroOt,r,UserAccessContext.FromClaims(p),ct)));
    }
    private static async Task<IResult> UploadEvidenceAsync(string numeroOt,string codigoTarea,HttpRequest request,ClaimsPrincipal principal,IWorkOrderService service,IDocumentStorageService storage,CmmsDbContext db,CancellationToken ct)
    {
        try
        {
            var form=await request.ReadFormAsync(ct);var file=form.Files.GetFile("file");if(file is null||file.Length==0)return Results.Problem(statusCode:400,detail:"Debe adjuntar un archivo.");if(file.Length>10*1024*1024)return Results.Problem(statusCode:400,detail:"El archivo supera 10 MB.");if(string.IsNullOrWhiteSpace(file.ContentType)||!file.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase))return Results.Problem(statusCode:400,detail:"Solo se permiten imágenes.");
            await using var stream=file.OpenReadStream();using var memory=new MemoryStream();await stream.CopyToAsync(memory,ct);var actor=UserAccessContext.FromClaims(principal);var saved=await storage.SaveEvidenceAsync(new DocumentStorageSaveRequest("WorkOrders","WorkOrderEvidence",$"{numeroOt}:{codigoTarea}",file.FileName,file.ContentType,memory.ToArray(),actor.UserId,DocumentStoragePurpose.Evidence,OtNumero:numeroOt),ct);var fileId=await db.Files.Where(x=>x.FileKey==saved.FileKey).Select(x=>x.Id).SingleAsync(ct);var response=await service.RegisterUploadedEvidenceAsync(numeroOt,codigoTarea,new UploadWorkOrderEvidenceRequest(form["tipo"].FirstOrDefault()??string.Empty,form["descripcion"].FirstOrDefault(),DateTimeOffset.TryParse(form["fechaCapturaUtc"].FirstOrDefault(),out var captured)?captured:null),fileId,actor,ct);return response is null?Results.NotFound():Results.Ok(response);
        }
        catch(Exception ex){return Error(ex);}
    }
    private static async Task<IResult> UploadSignatureAsync(string numeroOt,HttpRequest request,ClaimsPrincipal principal,IWorkOrderService service,IDocumentStorageService storage,CmmsDbContext db,CancellationToken ct)
    {
        try
        {
            var form=await request.ReadFormAsync(ct);var file=form.Files.GetFile("file");if(file is null||file.Length==0)return Results.Problem(statusCode:400,detail:"Debe adjuntar una firma.");if(file.Length>2*1024*1024)return Results.Problem(statusCode:400,detail:"La firma supera 2 MB.");if(string.IsNullOrWhiteSpace(file.ContentType)||!file.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase))return Results.Problem(statusCode:400,detail:"La firma debe ser imagen.");
            await using var stream=file.OpenReadStream();using var memory=new MemoryStream();await stream.CopyToAsync(memory,ct);var actor=UserAccessContext.FromClaims(principal);var saved=await storage.SaveEvidenceAsync(new DocumentStorageSaveRequest("WorkOrders","WorkOrderSignature",numeroOt,file.FileName,file.ContentType,memory.ToArray(),actor.UserId,DocumentStoragePurpose.Evidence,OtNumero:numeroOt),ct);var fileId=await db.Files.Where(x=>x.FileKey==saved.FileKey).Select(x=>x.Id).SingleAsync(ct);var response=await service.RegisterOwnSignatureAsync(numeroOt,new RegisterOwnWorkOrderSignatureRequest(form["comentario"].FirstOrDefault()),fileId,actor,ct);return response is null?Results.NotFound():Results.Ok(response);
        }
        catch(Exception ex){return Error(ex);}
    }
    private static async Task<IResult> One<T>(Func<Task<T?>> action) where T:class
    {try{var value=await action();return value is null?Results.NotFound():Results.Ok(value);}catch(Exception ex){return Error(ex);}}
    private static async Task<IResult> Many<T>(Func<Task<IReadOnlyCollection<T>>> action)
    {try{return Results.Ok(await action());}catch(Exception ex){return Error(ex);}}
    private static IResult Error(Exception ex)=>ex switch
    {UnauthorizedAccessException=>Results.Problem(statusCode:403,detail:ex.Message),DomainException=>Results.Problem(statusCode:422,detail:ex.Message),_=>Results.Problem(statusCode:500,title:"Error interno.")};
}
