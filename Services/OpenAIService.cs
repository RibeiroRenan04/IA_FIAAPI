using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SimplificadorLinguagem.API.DTOs;

namespace SimplificadorLinguagem.API.Services;

public class OpenAIService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    // Máximo de caracteres enviados para a API (~7.500 tokens para gpt-4o-mini)
    private const int MaxTextLength = 30_000;

    private const string SystemPrompt = """
        Você é um especialista em comunicação acessível no Brasil.
        Ao receber um texto, você deve:
        1. Identificar o tipo do texto: jurídico, médico, governamental ou geral
        2. Reescrever o conteúdo em linguagem simples, clara e acessível em português brasileiro, sem perder o sentido original
        3. Criar um resumo objetivo em 1 a 2 frases

        Responda EXCLUSIVAMENTE com um JSON válido neste formato (sem markdown, sem explicações):
        {
          "resultado": "versão simplificada do texto",
          "resumo": "resumo breve de 1-2 frases",
          "tipo": "jurídico|médico|governamental|geral"
        }
        """;

    private static readonly JsonSerializerOptions CaseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<SimplificarResponse> SimplificarAsync(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("O texto não pode ser vazio.");

        if (texto.Length > MaxTextLength)
            texto = texto[..MaxTextLength];

        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey não configurada.");

        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        var client = httpClientFactory.CreateClient("OpenAI");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = texto         }
            },
            temperature = 0.3,
            response_format = new { type = "json_object" }
        };

        var requestJson = JsonSerializer.Serialize(payload);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var httpResponse = await client.PostAsync("v1/chat/completions", requestContent);
        var responseBody  = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"OpenAI retornou {(int)httpResponse.StatusCode}: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var messageContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new InvalidOperationException("Resposta vazia da OpenAI.");

        return JsonSerializer.Deserialize<SimplificarResponse>(messageContent, CaseInsensitive)
            ?? throw new InvalidOperationException("Falha ao desserializar resposta da OpenAI.");
    }
}
