using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Costs;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Costs;
using Xunit;
namespace MaintenanceCMMS.Tests;
public sealed class CostManagementServiceTests
{
 private static readonly UserAccessContext Admin=new("admin",[AuthRoles.Admin],[AuthPermissions.ViewCosts],[]);
 [Fact] public async Task LaborAndExternalCosts_AreCalculatedAndPersisted(){await using var f=await PostgreSqlWorkTestFixture.CreateAsync();var s=new CostManagementService(f.DbContext,new PostgreSqlAuditService(f.DbContext,new AuditContextAccessor()));await s.UpsertRateAsync(new("GLOBAL",10000),Admin,default);var labor=await s.CreateAsync(new(CostCategory.ManoObra,0,"HH tecnico",DateTimeOffset.UtcNow,Cantidad:2),Admin,default);var external=await s.CreateAsync(new(CostCategory.ServicioExterno,50000,"Grua externa",DateTimeOffset.UtcNow,ProveedorRut:"76.1"),Admin,default);var dashboard=await s.DashboardAsync(new(),Admin,default);Assert.Equal(20000,labor.Monto);Assert.Equal(70000,dashboard.Total);Assert.Equal(50000,external.Monto);}
 [Fact] public async Task PaymentStatement_EnforcesStatusTransitions(){await using var f=await PostgreSqlWorkTestFixture.CreateAsync();var s=new CostManagementService(f.DbContext,new PostgreSqlAuditService(f.DbContext,new AuditContextAccessor()));var p=await s.CreatePaymentAsync(new("EP-1","76.1",100000,"CLP"),Admin,default);p=await s.ChangePaymentStatusAsync(p.NumeroEstadoPago,new(PaymentStatus.Enviado),Admin,default);p=await s.ChangePaymentStatusAsync(p!.NumeroEstadoPago,new(PaymentStatus.Aprobado),Admin,default);p=await s.ChangePaymentStatusAsync(p!.NumeroEstadoPago,new(PaymentStatus.Pagado),Admin,default);Assert.Equal(PaymentStatus.Pagado,p!.Estado);}
}