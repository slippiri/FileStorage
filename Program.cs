using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder();
var app = builder.Build();

// определение MIME-типа
static string GetMimeType(string filePath)
{
    return Path.GetExtension(filePath).ToLower() switch
    {
        ".txt" => "text/plain",
        ".html" => "text/html",
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}

var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "api", "file");
Directory.CreateDirectory(uploadPath);

// логирование метода и пути запросов
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.Run(async (context) =>
{
    var response = context.Response;
    var request = context.Request;
    var path = request.Path.Value ?? "";

    try
    {
        // обработка /api/file
        if (path.StartsWith("/api/file"))
        {
            var segments = path.Trim('/').Split('/');

            // GET список файлов
            if (segments.Length == 2 && request.Method == "GET")
            {
                var files = Directory.GetFiles(uploadPath).Select(Path.GetFileName);
                response.StatusCode = 200; // OK
                await response.WriteAsJsonAsync(files);
                return;
            }

            // Обработка операций с файлом
            if (segments.Length >= 3)
            {
                var fileName = Uri.UnescapeDataString(segments[2]);
                var filePath = Path.Combine(uploadPath, fileName);

                switch (request.Method)
                {
                    case "GET":
                        if (File.Exists(filePath))
                        {
                            response.StatusCode = 200;
                            response.ContentType = GetMimeType(filePath);

                            // добавляет заголовок для скачивания если нет параметра превью
                            if (!request.Query.ContainsKey("preview"))
                            {
                                response.Headers.Append("Content-Disposition",
                                    $"attachment; filename=\"{Uri.EscapeDataString(fileName)}\"");
                            }

                            await using var fs = new FileStream(filePath, FileMode.Open);
                            await fs.CopyToAsync(response.Body);
                        }
                        else
                        {
                            response.StatusCode = 404;
                            await response.WriteAsync("File not found");
                        }
                        break;

                    case "PUT":
                        await using (var fs = new FileStream(filePath, FileMode.Create))
                        {
                            await request.Body.CopyToAsync(fs); //сохраняет файл из тела запроса
                        }
                        response.StatusCode = 201;
                        break;

                    case "DELETE":
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            response.StatusCode = 204;
                        }
                        else
                        {
                            response.StatusCode = 404;
                        }
                        break;

                    case "HEAD":
                        if (File.Exists(filePath))
                        {
                            var info = new FileInfo(filePath);
                            response.Headers.Append("Content-Length", info.Length.ToString());
                            response.Headers.Append("Last-Modified", info.LastWriteTime.ToString("R"));
                            response.StatusCode = 200;
                        }
                        else
                        {
                            response.StatusCode = 404;
                        }
                        break;

                    default:
                        response.StatusCode = 405;
                        break;
                }
                return;
            }
        }

        var htmlPath = Path.Combine(Directory.GetCurrentDirectory(), "html", "index.html");
        await response.SendFileAsync(htmlPath);
    }
    catch (Exception ex)
    {
        response.StatusCode = 500;
        await response.WriteAsync($"Internal server error: {ex.Message}");
        Console.WriteLine($"ERROR: {ex}");
    }
});

app.Run();