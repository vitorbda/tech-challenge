import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';

import { mensagemDeErro } from '../nucleo/api';
import { Plano } from '../planos/plano';
import { PlanoServico } from '../planos/plano-servico';
import { Beneficiario, StatusBeneficiario } from './beneficiario';
import { BeneficiarioServico } from './beneficiario-servico';

/**
 * Mesmo algoritmo de `Beneficiario.CpfInvalido` no backend: 11 dígitos, nem todos iguais, e os
 * dois dígitos verificadores conferindo. Validar aqui evita uma ida à API para errar; a
 * validação do servidor continua sendo a que manda.
 */
function cpfValido(cpf: string): boolean {
  if (!/^[0-9]{11}$/.test(cpf) || new Set(cpf).size === 1) {
    return false;
  }

  const digitos = [...cpf].map(Number);

  const digitoVerificador = (pesoInicial: number): number => {
    let soma = 0;

    for (let i = 0; i < pesoInicial - 1; i++) {
      soma += digitos[i] * (pesoInicial - i);
    }

    const resto = ((soma * 10) % 11) as number;

    return resto === 10 ? 0 : resto;
  };

  return digitos[9] === digitoVerificador(10) && digitos[10] === digitoVerificador(11);
}

function validadorDeCpf(controle: AbstractControl): ValidationErrors | null {
  const valor = (controle.value ?? '') as string;

  return !valor || cpfValido(valor) ? null : { cpf: true };
}

/** A API recusa data de nascimento maior ou igual a hoje, então o limite é ontem. */
function ontemComoTexto(): string {
  const data = new Date();
  data.setDate(data.getDate() - 1);

  return data.toISOString().slice(0, 10);
}

function hojeComoTexto(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * O atributo `max` do input de data só limita o seletor nativo: digitar ou colar uma data
 * futura passa direto. A regra do backend (`data_nascimento >= hoje` é recusada) precisa de
 * validador próprio. Comparar como texto funciona porque 'YYYY-MM-DD' ordena lexicograficamente.
 */
function validadorDeDataPassada(controle: AbstractControl): ValidationErrors | null {
  const valor = (controle.value ?? '') as string;

  return !valor || valor < hojeComoTexto() ? null : { dataNaoPassada: true };
}

@Component({
  selector: 'app-beneficiario-formulario',
  imports: [ReactiveFormsModule],
  templateUrl: './beneficiario-formulario.html',
  styleUrl: './beneficiario-formulario.css'
})
export class BeneficiarioFormulario implements OnInit {
  private readonly servico = inject(BeneficiarioServico);
  private readonly planoServico = inject(PlanoServico);
  private readonly fb = inject(FormBuilder);
  // takeUntilDestroyed sem argumento só funciona em contexto de injeção. Fora do construtor
  // é preciso passar o DestroyRef explicitamente.
  private readonly destroyRef = inject(DestroyRef);

  /** `null` cadastra; preenchido edita. */
  readonly id = input<string | null>(null);

  readonly salvo = output<Beneficiario>();
  readonly cancelado = output<void>();

  protected readonly planos = signal<Plano[]>([]);
  protected readonly carregando = signal(false);
  protected readonly enviando = signal(false);
  protected readonly erro = signal<string | null>(null);

  /** Status que veio do servidor, para avisar sobre o congelamento do registro inativo. */
  protected readonly statusOriginal = signal<StatusBeneficiario | null>(null);

  protected readonly edicao = computed(() => this.id() !== null);
  protected readonly maximoDeNascimento = ontemComoTexto();

  protected readonly formulario = this.fb.nonNullable.group({
    nome_completo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    cpf: ['', [Validators.required, validadorDeCpf]],
    data_nascimento: ['', [Validators.required, validadorDeDataPassada]],
    plano_id: ['', [Validators.required]],
    status: ['ATIVO' as StatusBeneficiario]
  });

  constructor() {
    this.planoServico
      .listar()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (planos) => this.planos.set(planos),
        error: (resposta: HttpErrorResponse) => this.erro.set(mensagemDeErro(resposta))
      });
  }

  // O valor de um input só está disponível depois da construção do componente: lido no
  // construtor, `id()` devolveria sempre o padrão e a edição carregaria o registro errado.
  ngOnInit(): void {
    const id = this.id();

    if (id !== null) {
      this.carregar(id);
    }
  }

  /**
   * Registro inativo é congelado no backend: alterar dados sem reativar responde 409. O aviso
   * existe para a pessoa saber disso antes de preencher, não depois de levar o erro.
   */
  protected precisaReativar(): boolean {
    return (
      this.statusOriginal() === 'INATIVO' && this.formulario.controls.status.value === 'INATIVO'
    );
  }

  protected enviar(): void {
    if (this.formulario.invalid || this.enviando()) {
      return;
    }

    this.enviando.set(true);
    this.erro.set(null);

    const valores = this.formulario.getRawValue();
    const id = this.id();

    // Corpo montado à mão, e não form.value: assim o CPF nunca escapa para o PUT, que a API
    // ignora, e nenhum campo controlado pelo servidor é enviado no POST.
    const requisicao =
      id === null
        ? this.servico.criar({
            nome_completo: valores.nome_completo.trim(),
            cpf: valores.cpf.trim(),
            data_nascimento: valores.data_nascimento,
            plano_id: valores.plano_id
          })
        : this.servico.atualizar(id, {
            nome_completo: valores.nome_completo.trim(),
            data_nascimento: valores.data_nascimento,
            plano_id: valores.plano_id,
            status: valores.status
          });

    requisicao.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (beneficiario) => {
        this.enviando.set(false);
        this.salvo.emit(beneficiario);
      },
      error: (resposta: HttpErrorResponse) => {
        this.erro.set(mensagemDeErro(resposta));
        this.enviando.set(false);
      }
    });
  }

  private carregar(id: string): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.servico
      .obter(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (beneficiario) => {
          this.formulario.setValue({
            nome_completo: beneficiario.nome_completo,
            cpf: beneficiario.cpf,
            // A data vem como 'YYYY-MM-DD' e o input date usa o mesmo formato. Converter para
            // Date no meio do caminho faria a data andar um dia por causa do fuso.
            data_nascimento: beneficiario.data_nascimento,
            plano_id: beneficiario.plano_id,
            status: beneficiario.status
          });
          this.statusOriginal.set(beneficiario.status);
          this.carregando.set(false);
        },
        error: (resposta: HttpErrorResponse) => {
          this.erro.set(mensagemDeErro(resposta));
          this.carregando.set(false);
        }
      });
  }
}
