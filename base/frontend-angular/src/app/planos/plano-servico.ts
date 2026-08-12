import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE } from '../nucleo/api';
import { Plano } from './plano';

/**
 * Todo acesso à API passa por um serviço. Componente não chama HttpClient direto.
 */
@Injectable({ providedIn: 'root' })
export class PlanoServico {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE);

  listar(): Observable<Plano[]> {
    return this.http.get<Plano[]>(`${this.base}/planos`);
  }
}
