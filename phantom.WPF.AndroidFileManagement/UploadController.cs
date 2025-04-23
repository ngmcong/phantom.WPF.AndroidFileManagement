using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Security.Cryptography;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class UploadChunkController : ControllerBase
{
    private readonly string _uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads"); // Temporary storage

    public UploadChunkController()
    {
        if (!Directory.Exists(_uploadDirectory))
        {
            Directory.CreateDirectory(_uploadDirectory);
        }
    }

    private async Task<string> CalculateMD5Async(string filePath)
    {
        try
        {
            using (FileStream fileStream = System.IO.File.OpenRead(filePath))
            {
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hashBytes = await Task.Run(() => md5.ComputeHash(fileStream));
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        sb.Append(hashBytes[i].ToString("x2")); // To get hexadecimal string
                    }
                    return sb.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calculating MD5: {ex.Message}");
            return string.Empty; // Or throw an exception if you prefer
        }
    }

    [HttpPost("uploadchunk")]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)] // Important for large files
    [DisableRequestSizeLimit] // Also important
    public async Task<IActionResult> UploadChunk()
    {
        try
        {
            var fileChunk = Request.Form.Files.FirstOrDefault();
            var fileName = Request.Form["fileName"].FirstOrDefault();
            var totalSize = long.Parse(Request.Form["totalSize"].FirstOrDefault() ?? "");
            var offset = int.Parse(Request.Form["offset"].FirstOrDefault() ?? "");
            var partNumber = int.Parse(Request.Form["partNumber"].FirstOrDefault() ?? "");
            var totalParts = int.Parse(Request.Form["totalParts"].FirstOrDefault() ?? "");

            if (fileChunk == null || fileChunk.Length == 0 || string.IsNullOrEmpty(fileName))
            {
                return BadRequest("Invalid chunk data.");
            }

            var tempFilePath = Path.Combine(_uploadDirectory, $"{fileName}.part_{partNumber}");

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await fileChunk.CopyToAsync(stream);
            }

            // Check if all parts have been uploaded
            var allParts = Directory.GetFiles(_uploadDirectory, $"{fileName}.part_*")
                                    .OrderBy(f => int.Parse(Path.GetFileName(f).Split('_').Last()));

            if (allParts.Count() == totalParts)
            {
                // Reassemble the file
                var finalFilePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", fileName);
                using (var finalStream = new FileStream(finalFilePath, FileMode.Create))
                {
                    foreach (var partPath in allParts)
                    {
                        using (var partStream = new FileStream(partPath, FileMode.Open))
                        {
                            await partStream.CopyToAsync(finalStream);
                        }
                        System.IO.File.Delete(partPath); // Clean up temporary parts
                    }
                }
                return Ok(System.Text.Json.JsonSerializer.Serialize(new
                {
                    Message = "File uploaded successfully",
                    FileName = fileName,
                    FilePath = finalFilePath,
                    MD5Hash = await CalculateMD5Async(finalFilePath),
                }));
            }

            return Ok(System.Text.Json.JsonSerializer.Serialize(new { Message = $"Chunk {partNumber} received" }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error processing chunk: {ex.Message}");
        }
    }
}