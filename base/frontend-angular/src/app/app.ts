import { Component } from '@angular/core';

import { PlanosLista } from './planos/planos-lista';

@Component({
  selector: 'app-root',
  imports: [PlanosLista],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
