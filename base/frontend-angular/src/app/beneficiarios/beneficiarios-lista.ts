import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, switchMap } from 'rxjs';

import { mensagemDeErro } from '../nucleo/api';
import { Plano } from '../planos/plano';
import { PlanoServico } from '../planos/plano-servico';
import { Beneficiario, StatusBeneficiario } from './beneficiario';
import { BeneficiarioServico, FiltrosBeneficiario } from './beneficiario-servico';

/** Quantidade de itens por página. Enviada sempre, para não depender do padrão da API. */
const TAMANHO_DA_PAGINA = 10;

@Component({
  selector: 'app-beneficiarios-lista',
  templateUrl: './beneficiarios-lista.html',
  styleUrl: './beneficiarios-lista.css'
})
export class BeneficiariosLista {
  private readonly servico = inject(BeneficiarioServico);
  private readonly planoServico = inject(PlanoServico);

  readonly editar = output<Beneficiario>();
  readonly excluir = output<Beneficiario>();

  protected readonly beneficiarios = signal<Beneficiario[]>([]);
  protected readonly total = signal(0);
  protected readonly carregando = signal(true);
  protected readonly erro = signal<string | null>(null);

  protected readonly pagina = signal(1);
  protected readonly status = signal<StatusBeneficiario | null>(null);
  protected readonly planoId = signal<string | null>(null);

  /** Alimenta o select de filtro por plano. Carregado uma vez, não a cada filtro. */
  protected readonly planos = signal<Plano[]>([]);

  // Distingue "os planos ainda não chegaram" de "este plano não existe mais". Sem isso, a
  // primeira renderização mostraria o texto de plano excluído em todas as linhas.
  private readonly planosCarregados = signal(false);

  /** Falha ao carregar os planos não derruba a tabela: só os nomes ficam sem resolver. */
  protected readonly erroDePlanos = signal<string | null>(null);

  // Cruzar por id em vez de procurar no array a cada linha, que seria o mesmo N+1 do
  // backend, agora no navegador.
  private readonly nomesPorPlano = computed(
    () => new Map(this.planos().map((plano) => [plano.id, plano.nome]))
  );

  protected readonly tamanho = TAMANHO_DA_PAGINA;

  // Cada mudança de filtro empurra os filtros aqui. O switchMap cancela a requisição
  // anterior, senão a resposta lenta de um filtro antigo sobrescreve a do filtro atual.
  private readonly filtros = new Subject<FiltrosBeneficiario>();

  constructor() {
    this.filtros
      .pipe(
        switchMap((filtros) => this.servico.listar(filtros)),
        takeUntilDestroyed()
      )
      .subscribe({
        next: (pagina) => {
          this.beneficiarios.set(pagina.dados);
          this.total.set(pagina.total);
          this.carregando.set(false);
        },
        error: (resposta: HttpErrorResponse) => {
          this.erro.set(mensagemDeErro(resposta));
          this.carregando.set(false);
        }
      });

    this.planoServico
      .listar()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (planos) => {
          this.planos.set(planos);
          this.planosCarregados.set(true);
        },
        error: (resposta: HttpErrorResponse) => this.erroDePlanos.set(mensagemDeErro(resposta))
      });

    this.carregar();
  }

  /** Última página com resultado. Sempre pelo menos 1, para não sumir a paginação. */
  protected ultimaPagina(): number {
    return Math.max(1, Math.ceil(this.total() / this.tamanho));
  }

  /**
   * Nome do plano do beneficiário. `GET /planos` não devolve plano excluído, mas o
   * beneficiário continua vinculado a ele, então a chave pode faltar.
   */
  protected nomeDoPlano(planoId: string): string {
    if (!this.planosCarregados()) {
      return '—';
    }

    return this.nomesPorPlano().get(planoId) ?? 'Plano descontinuado';
  }

  protected temFiltro(): boolean {
    return this.status() !== null || this.planoId() !== null;
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.filtros.next({
      pagina: this.pagina(),
      tamanho: this.tamanho,
      status: this.status(),
      planoId: this.planoId()
    });
  }

  protected mudarStatus(valor: string): void {
    this.status.set(valor ? (valor as StatusBeneficiario) : null);
    this.aplicarFiltro();
  }

  protected mudarPlano(valor: string): void {
    this.planoId.set(valor || null);
    this.aplicarFiltro();
  }

  protected irPara(pagina: number): void {
    if (pagina < 1 || pagina > this.ultimaPagina()) {
      return;
    }

    this.pagina.set(pagina);
    this.carregar();
  }

  // Filtrar volta para a primeira página. Sem isso, filtrar estando na página 3 deixaria a
  // tela vazia sempre que o novo conjunto tivesse menos páginas que a atual.
  private aplicarFiltro(): void {
    this.pagina.set(1);
    this.carregar();
  }
}
