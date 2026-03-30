using DevExpress.AspNetCore;
using DevExpress.AspNetCore.Reporting;
using DevExpress.Security.Resources;
using DevExpress.XtraCharts;
using DevExpress.XtraReports.Web.Extensions;
using DXApplication1.Services;
using ESS.Platform.Authorization.Authentication;
using ESS.Platform.Authorization.Dependencies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

AppDomain.CurrentDomain.SetData("DataDirectory", builder.Environment.ContentRootPath);
builder.Services.AddDevExpressControls();

// Register Azure Blob Storage service
builder.Services.AddSingleton<IAzureBlobStorageService, AzureBlobStorageService>();

builder.Services.AddClaimResolverServiceCollection();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddScheme<JwtBearerOptions, JwtAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ReportStorageWebExtension, CustomReportStorageWebExtension>();
builder.Services.AddMvc();
builder.Services.AddControllers();
builder.Services.AddCors();

builder.Services.ConfigureReportingServices(configurator =>
{
    if (builder.Environment.IsDevelopment())
        configurator.UseDevelopmentMode();

    configurator.ConfigureReportDesigner(designerConfigurator =>
    {
        // Register your API-based connection provider for the designer
        designerConfigurator.RegisterDataSourceWizardJsonConnectionStorage<CustomApiDataConnectionStorage>(true);
    });
    configurator.ConfigureWebDocumentViewer(viewerConfigurator =>
    {
        viewerConfigurator.UseCachedReportSourceBuilder();
        // Register your API-based connection provider for the viewer
        // This also enables Data Federation - FederationDataSource uses the same JSON connections
        viewerConfigurator.RegisterJsonDataConnectionProviderFactory<CustomJsonDataConnectionProviderFactory>();
    });
});

var app = builder.Build();
var contentDirectoryAllowRule = DirectoryAccessRule.Allow(new DirectoryInfo(Path.Combine(app.Environment.ContentRootPath, "Content")).FullName);
AccessSettings.ReportingSpecificResources.SetRules(contentDirectoryAllowRule, UrlAccessRule.Deny());
DevExpress.XtraReports.Configuration.Settings.Default.UserDesignerOptions.DataBindingMode = DevExpress.XtraReports.UI.DataBindingMode.Expressions;

app.UseHttpsRedirection();
app.UseRouting();

// Add Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
app.UseDevExpressControls();
System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
app.UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());

app.Run();