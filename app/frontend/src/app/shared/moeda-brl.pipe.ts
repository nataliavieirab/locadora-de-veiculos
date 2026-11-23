import { Pipe, PipeTransform } from '@angular/core';

const reais = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

@Pipe({ name: 'moedaBrl' })
export class MoedaBrlPipe implements PipeTransform {
  transform(valor: string): string {
    if (!/^\d+(\.\d+)?$/.test(valor)) return 'Valor inválido';
    const [inteiro, fracao = ''] = valor.split('.');
    const casas = fracao.padEnd(3, '0');
    const centavos = BigInt(inteiro) * 100n + BigInt(casas.slice(0, 2)) + (casas[2] >= '5' ? 1n : 0n);
    return reais.formatToParts(centavos / 100n).map((parte) =>
      parte.type === 'fraction' ? (centavos % 100n).toString().padStart(2, '0') : parte.value,
    ).join('');
  }
}
