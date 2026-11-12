
using CallLect.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddScoped<DbConnection>();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant();

    if (path != null &&
        (path.StartsWith("/login") ||
         path.StartsWith("/register") ||
         path.StartsWith("/css") ||
         path.StartsWith("/js") ||
         path.StartsWith("/lib") ||
         path == "/favicon.ico"))
    {
        await next();
        return;
    }

    if (string.IsNullOrEmpty(context.Session.GetString("LoggedIn")))
    {
        context.Response.Redirect("/Login");
        return;
    }

    await next();
});

app.MapRazorPages();

app.Run();
