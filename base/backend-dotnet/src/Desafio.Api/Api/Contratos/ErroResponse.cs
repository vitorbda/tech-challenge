using Desafio.Api.Dominio;

namespace Desafio.Api.Api.Contratos;

public sealed record ErroResponse(string Erro, string Mensagem, IReadOnlyList<DetalheErro> Detalhes)
{
    public static ErroResponse De(ExcecaoDeDominio excecao) =>
        new(excecao.Erro, excecao.Message, excecao.Detalhes);
}
