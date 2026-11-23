import { HttpErrorResponse } from '@angular/common/http';

export interface ProblemaCadastro {
  title?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export function problemaCadastro(erro: unknown): ProblemaCadastro {
  if (!(erro instanceof HttpErrorResponse)) return { title: 'Não foi possível concluir a operação.' };
  if (erro.status === 412 || erro.status === 428) {
    return { title: 'O cadastro foi alterado ou está sem versão. Reabra o formulário para recarregar os dados.' };
  }
  if (erro.status === 0 || erro.status >= 500) {
    return { title: 'Não foi possível confirmar a operação. Confira os registros antes de tentar novamente.' };
  }
  const problema = erro.error as ProblemaCadastro | null;
  return { title: problema?.title ?? 'Não foi possível concluir a operação.', code: problema?.code, errors: problema?.errors };
}
