export type StatusBeneficiario = 'ATIVO' | 'INATIVO';

export interface Beneficiario {
  id: string;
  nome_completo: string;
  cpf: string;
  data_nascimento: string;
  status: StatusBeneficiario;
  plano_id: string;
  data_cadastro: string;
}

export interface Pagina<T> {
  dados: T[];
  pagina: number;
  tamanho: number;
  total: number;
}

export interface NovoBeneficiario {
  nome_completo: string;
  cpf: string;
  data_nascimento: string;
  plano_id: string;
}

export interface AtualizacaoBeneficiario {
  nome_completo: string;
  data_nascimento: string;
  plano_id: string;
  status: StatusBeneficiario;
}
