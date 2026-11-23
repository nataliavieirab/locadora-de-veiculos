import { MoedaBrlPipe } from './moeda-brl.pipe';

describe('MoedaBrlPipe', () => {
  const pipe = new MoedaBrlPipe();
  const formatar = (valor: string) => pipe.transform(valor).replace(/\u00a0/g, ' ');

  it.each([
    ['0', 'R$ 0,00'],
    ['118.000000', 'R$ 118,00'],
    ['1.004999', 'R$ 1,00'],
    ['1.005', 'R$ 1,01'],
    ['999.999999', 'R$ 1.000,00'],
    ['9999999999999.994999', 'R$ 9.999.999.999.999,99'],
    ['9999999999999.995', 'R$ 10.000.000.000.000,00'],
  ])('formata %s sem perda de precisão decimal', (entrada, esperado) => {
    expect(formatar(entrada)).toBe(esperado);
  });

  it.each(['', '-1', '1,20', 'NaN', '1e3'])('não inventa preço para %s', (entrada) => {
    expect(pipe.transform(entrada)).toBe('Valor inválido');
  });
});
