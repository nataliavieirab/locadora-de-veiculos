export { focarResumo } from '../../shared/foco';

export function mensagemCampo(erros: readonly { message?: string }[]): string {
  return erros[0]?.message ?? 'Revise este campo.';
}
