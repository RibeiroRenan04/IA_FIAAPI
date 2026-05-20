namespace SimplificadorLinguagem.API.DTOs;

public record SimplificarTextoRequest(string Texto);

public record SimplificarResponse(string Resultado, string? Resumo, string? Tipo);
