import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE } from '../nucleo/api';
import {
  AtualizacaoBeneficiario,
  Beneficiario,
  NovoBeneficiario,
  Pagina,
  StatusBeneficiario
} from './beneficiario';

export interface FiltrosBeneficiario {
  pagina?: number;
  tamanho?: number;
  status?: StatusBeneficiario | null;
  planoId?: string | null;
}

@Injectable({ providedIn: 'root' })
export class BeneficiarioServico {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE);

  listar(filtros: FiltrosBeneficiario = {}): Observable<Pagina<Beneficiario>> {
    let params = new HttpParams();

    if (filtros.pagina) {
      params = params.set('pagina', filtros.pagina);
    }

    if (filtros.tamanho) {
      params = params.set('tamanho', filtros.tamanho);
    }

    if (filtros.status) {
      params = params.set('status', filtros.status);
    }

    if (filtros.planoId) {
      params = params.set('plano_id', filtros.planoId);
    }

    return this.http.get<Pagina<Beneficiario>>(`${this.base}/beneficiarios`, { params });
  }

  obter(id: string): Observable<Beneficiario> {
    return this.http.get<Beneficiario>(`${this.base}/beneficiarios/${id}`);
  }

  criar(dados: NovoBeneficiario): Observable<Beneficiario> {
    return this.http.post<Beneficiario>(`${this.base}/beneficiarios`, dados);
  }

  atualizar(id: string, dados: AtualizacaoBeneficiario): Observable<Beneficiario> {
    return this.http.put<Beneficiario>(`${this.base}/beneficiarios/${id}`, dados);
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/beneficiarios/${id}`);
  }
}
