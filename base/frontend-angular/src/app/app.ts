import { Component, signal } from '@angular/core';

import { Beneficiario } from './beneficiarios/beneficiario';
import { BeneficiarioFormulario } from './beneficiarios/beneficiario-formulario';
import { BeneficiariosLista } from './beneficiarios/beneficiarios-lista';
import { PlanosLista } from './planos/planos-lista';

@Component({
  selector: 'app-root',
  imports: [PlanosLista, BeneficiariosLista, BeneficiarioFormulario],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly modo = signal<'lista' | 'formulario'>('lista');
  protected readonly emEdicao = signal<string | null>(null);

  protected abrirCadastro(): void {
    this.emEdicao.set(null);
    this.modo.set('formulario');
  }

  protected abrirEdicao(beneficiario: Beneficiario): void {
    this.emEdicao.set(beneficiario.id);
    this.modo.set('formulario');
  }

  protected voltarParaLista(): void {
    this.modo.set('lista');
  }
}
