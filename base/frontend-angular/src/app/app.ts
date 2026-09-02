import { Component } from '@angular/core';

import { BeneficiariosLista } from './beneficiarios/beneficiarios-lista';
import { PlanosLista } from './planos/planos-lista';

@Component({
  selector: 'app-root',
  imports: [PlanosLista, BeneficiariosLista],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
