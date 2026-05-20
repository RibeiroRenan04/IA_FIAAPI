using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using SimplificadorLinguagem.API.DTOs;
using SimplificadorLinguagem.API.Services;
using UglyToad.PdfPig;

namespace SimplificadorLinguagem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradutorController(
    OpenAIService openAI,
    ILogger<TradutorController> logger) : ControllerBase
{
    // ── POST /api/tradutor/simplificar ────────────────────────────────────────
    [HttpPost("simplificar")]
    public async Task<IActionResult> SimplificarTexto([FromBody] SimplificarTextoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Texto))
            return BadRequest(new { error = "O campo 'texto' é obrigatório." });

        try
        {
            var resultado = await openAI.SimplificarAsync(request.Texto);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao simplificar texto");
            return StatusCode(500, new { error = "Erro interno ao processar o texto." });
        }
    }

    // ── POST /api/tradutor/simplificar-arquivo ────────────────────────────────
    [HttpPost("simplificar-arquivo")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> SimplificarArquivo(IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { error = "Nenhum arquivo enviado." });

        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".docx" or ".txt"))
            return BadRequest(new { error = "Formato não suportado. Use .pdf, .docx ou .txt." });

        string texto;
        try
        {
            texto = ext switch
            {
                ".txt"  => await ExtrairTxtAsync(arquivo),
                ".pdf"  => ExtrairPdf(arquivo),
                ".docx" => ExtrairDocx(arquivo),
                _       => throw new NotSupportedException()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao extrair texto do arquivo {Nome}", arquivo.FileName);
            return BadRequest(new { error = "Não foi possível extrair o texto do arquivo." });
        }

        if (string.IsNullOrWhiteSpace(texto))
            return BadRequest(new { error = "O arquivo não contém texto legível." });

        try
        {
            var resultado = await openAI.SimplificarAsync(texto);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao simplificar arquivo {Nome}", arquivo.FileName);
            return StatusCode(500, new { error = "Erro interno ao processar o arquivo." });
        }
    }

    // ── Extratores ────────────────────────────────────────────────────────────

    private static async Task<string> ExtrairTxtAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static string ExtrairPdf(IFormFile file)
    {
        using var mem = CopiarParaMemoria(file);
        using var pdf = PdfDocument.Open(mem);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static string ExtrairDocx(IFormFile file)
    {
        using var mem = CopiarParaMemoria(file);
        using var wordDoc = WordprocessingDocument.Open(mem, isEditable: false);
        var body = wordDoc.MainDocumentPart?.Document.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
            sb.AppendLine(para.InnerText);
        return sb.ToString();
    }

    /// <summary>Copia o stream do IFormFile para um MemoryStream buscável.</summary>
    private static MemoryStream CopiarParaMemoria(IFormFile file)
    {
        var mem = new MemoryStream();
        file.OpenReadStream().CopyTo(mem);
        mem.Position = 0;
        return mem;
    }
}
