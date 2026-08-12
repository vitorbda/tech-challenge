import { HttpErrorResponse } from '@angular/common/http';
import { InjectionToken } from '@angular/core';

/**
 * Endereço da API. Fica em um token de injeção, e não escrito direto no serviço, para que dê
 * para trocar em teste e em outro ambiente sem mexer no código que consome.
 */
export const API_BASE = new InjectionToken<string>('API_BASE');

export const API_BASE_PADRAO = 'http://localhost:9999';

/** Formato do corpo de erro devolvido pela API. */
export interface ErroDaApi {
  erro: string;
  mensagem: string;
  detalhes: { campo: string; regra: string }[];
}

/**
 * Traduz a resposta de erro da API em uma frase para a tela.
 *
 * A API sempre devolve `{ erro, mensagem, detalhes }`, então dá para aproveitar o campo
 * recusado em vez de mostrar um "algo deu errado" genérico.
 */
export function mensagemDeErro(resposta: HttpErrorResponse): string {
  if (resposta.status === 0) {
    return 'Não foi possível falar com a API. Confira se ela está no ar.';
  }

  const corpo = resposta.error as ErroDaApi | null;

  if (!corpo?.mensagem) {
    return `A API respondeu ${resposta.status}.`;
  }

  if (corpo.detalhes?.length) {
    const campos = corpo.detalhes.map((d) => `${d.campo} (${d.regra})`).join(', ');
    return `${corpo.mensagem}: ${campos}`;
  }

  return corpo.mensagem;
}
