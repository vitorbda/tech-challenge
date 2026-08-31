namespace Desafio.Api.Api.Contratos;

public sealed record BeneficiarioRequest(
    string? NomeCompleto, string? Cpf, DateOnly? DataNascimento, Guid? PlanoId);
