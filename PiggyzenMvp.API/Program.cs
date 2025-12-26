using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using PiggyzenMvp.API.Data;
using PiggyzenMvp.API.Services;
using PiggyzenMvp.API.Services.Imports;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<NormalizeService>();
builder.Services.AddScoped<DescriptionSignatureService>();
builder.Services.AddScoped<TransactionImportService>();
builder.Services.AddScoped<CategorizationService>();
builder.Services.AddScoped<CategorySlugService>();
builder.Services.AddScoped<CategorySeeder>();
builder.Services.AddSingleton<ImportConfigService>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<ImportConfigService>().GetAsync(null).GetAwaiter().GetResult());
builder.Services.AddSingleton<TransactionKindMapper>();
builder.Services.AddDbContext<PiggyzenMvpContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"))
);
builder.Services.AddControllers(options =>
{
    options.InputFormatters.Insert(0, new TextPlainInputFormatter());
});

builder.Services.AddControllers();

// Klassisk Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Piggyzen API", Version = "v1" });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<CategorySeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error-development");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/error");
}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Custom TextPlainInputFormatter class
public class TextPlainInputFormatter : TextInputFormatter
{
    public TextPlainInputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/plain"));
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override bool CanReadType(Type type)
    {
        return type == typeof(string);
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context,
        Encoding encoding
    )
    {
        using var reader = new StreamReader(context.HttpContext.Request.Body, encoding);
        var content = await reader.ReadToEndAsync();
        return await InputFormatterResult.SuccessAsync(content);
    }
}
