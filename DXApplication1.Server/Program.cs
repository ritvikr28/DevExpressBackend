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

// Set DataDirectory for DevExpress to locate data files relative to content root
AppDomain.CurrentDomain.SetData("DataDirectory", builder.Environment.ContentRootPath);
builder.Services.AddDevExpressControls();

// Register Azure Blob Storage service
builder.Services.AddSingleton<IAzureBlobStorageService, AzureBlobStorageService>();

// Register in-memory report session store (maps tokens to learner ID arrays)
builder.Services.AddSingleton<ReportSessionStore>();

builder.Services.AddHttpClient();

builder.Services.AddClaimResolverServiceCollection();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddScheme<JwtBearerOptions, JwtAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ReportStorageWebExtension, CustomReportStorageWebExtension>();
builder.Services.AddMvc();
builder.Services.AddControllers();

// Configure CORS with allowed origins from configuration
var allowedOrigins = builder.Configuration.GetValue<string>("AllowedOrigins");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!string.IsNullOrEmpty(allowedOrigins))
        {
            var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins);
        }
        else
        {
            // In development, allow any origin; in production, require explicit configuration
            policy.AllowAnyOrigin();
        }
        policy.AllowAnyMethod();
        policy.AllowAnyHeader();
    });
});

builder.Services.ConfigureReportingServices(configurator =>
{
    if (builder.Environment.IsDevelopment())
        configurator.UseDevelopmentMode();

    configurator.ConfigureReportDesigner(designerConfigurator =>
    {
        // Register your API-based connection provider for the designer
        designerConfigurator.RegisterDataSourceWizardJsonConnectionStorage<CustomApiDataConnectionStorage>(true);
        
        // NOTE: Data source wizard buttons (Add SQL/Object/JSON/Federated DataSource) are hidden
        // via the frontend CustomizeMenuActions callback in the React ReportDesigner component.
        // DevExpress 25.2.x does not expose DataSourceWizardSettings on the server-side
        // ReportDesignerConfigurationBuilder. The frontend approach using action.visible = false
        // effectively hides these buttons from users while maintaining the backend API capability.
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

// Configure content directory access for DevExpress resources (if the Content directory exists)
var contentDirectoryPath = Path.Combine(app.Environment.ContentRootPath, "Content");
if (Directory.Exists(contentDirectoryPath))
{
    var contentDirectoryAllowRule = DirectoryAccessRule.Allow(new DirectoryInfo(contentDirectoryPath).FullName);
    AccessSettings.ReportingSpecificResources.SetRules(contentDirectoryAllowRule, UrlAccessRule.Deny());
}

DevExpress.XtraReports.Configuration.Settings.Default.UserDesignerOptions.DataBindingMode = DevExpress.XtraReports.UI.DataBindingMode.Expressions;

app.UseHttpsRedirection();
app.UseRouting();

// Add Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseCors();
app.UseDevExpressControls();
System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
app.UseEndpoints(endpoints => endpoints.MapControllers());

app.Run();