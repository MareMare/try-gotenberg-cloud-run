using System.Diagnostics;
using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;

const string cloudRunUrl = "https://try-gotenberg-cloud-run-523801905704.asia-northeast2.run.app";
const string inputFilePath = "SampleFiles/2025年度年間休日表.xlsx";
const string outputFilePath = "年度年間休日表.pdf";

Console.WriteLine($"{DateTime.Now:HH:mm:ss}: Hello, try-gotenberg-cloud-run!");
try
{
    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: Starting ID token retrieval...");
    var idToken = await GetIdTokenAsync(cloudRunUrl).ConfigureAwait(false);

    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: ID token retrieved successfully.");
    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: Creating HttpClient with Bearer token...");

    using var httpClient = CreateHttpClient(idToken);

    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: HttpClient created successfully.");
    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: Starting XLSX to PDF conversion...");
    
    await httpClient.ConvertXlsxToPdfAsync(cloudRunUrl, inputFilePath, outputFilePath).ConfigureAwait(false);

    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: PDF conversion successful. File saved as '{outputFilePath}'.");
    var startInfo = new ProcessStartInfo
    {
        FileName = Path.GetFullPath(outputFilePath),
        UseShellExecute = true,
    };
    Process.Start(startInfo);
}
catch (Exception ex)
{
    Console.WriteLine($"{DateTime.Now:HH:mm:ss}: An error occurred: {ex.Message}");
}

return;

static async ValueTask<string> GetIdTokenAsync(string audience)
{
    // 1. Google 認証情報の取得（環境変数 GOOGLE_APPLICATION_CREDENTIALS のパスを参照）
    // Audience には Cloud Run の URL を指定します
    var googleCredential = await GoogleCredential.GetApplicationDefaultAsync().ConfigureAwait(false);

    // Cloud Run の IAM 認証には ID トークンが必要です
    // OidcToken オプションを使用して、特定の Audience 用のトークンを取得
    var oidcToken = await googleCredential.GetOidcTokenAsync(OidcTokenOptions.FromTargetAudience(audience));

    // 2. トークンの取得（有効期限内であればキャッシュされたものが返り、切れていれば自動リニューアルされる）
    return await oidcToken.GetAccessTokenAsync().ConfigureAwait(false);
}

static HttpClient CreateHttpClient(string bearerToken)
{
    var httpClient = new HttpClient();
    // 3. ヘッダーに Bearer トークンをセット
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    return httpClient;
}

file static class HttpClientExtensions
{
    extension(HttpClient httpClient)
    {
        public async ValueTask ConvertXlsxToPdfAsync(string gotenbergUrl, string inputXlsxFilePath, string outputPdfFilePath)
        {
            var xlsxBytes = await ReadXlsxFileAsBytesAsync(inputXlsxFilePath).ConfigureAwait(false);
            var pdfBytes = await httpClient.ConvertXlsxToPdfAsync(gotenbergUrl, xlsxBytes ?? []).ConfigureAwait(false);
            await File.WriteAllBytesAsync(outputPdfFilePath, pdfBytes ?? []);
        }

        public async ValueTask<byte[]?> ConvertXlsxToPdfAsync(string gotenbergUrl, byte[] xlsxBytes)
        {
            // 4. Gotenberg (xlsx → pdf) へのリクエスト構築
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(xlsxBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "files", "document.xlsx");

            // Gotenberg の LibreOffice 変換エンドポイント
            var response = await httpClient.PostAsync($"{gotenbergUrl}/forms/libreoffice/convert", content);
            if (response.IsSuccessStatusCode)
            {
                var pdfBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return pdfBytes;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gotenberg Error: {error}");
            }
        }
    }

    private static async ValueTask<byte[]?> ReadXlsxFileAsBytesAsync(string xlsxFilePath)
    {
        await using var fileStream = File.OpenRead(xlsxFilePath);
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream).ConfigureAwait(false);
        return memoryStream.ToArray();
    }
}
