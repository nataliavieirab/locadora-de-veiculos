import { datetimeLocalSaoPaulo, formatarInstanteSp, sugestaoRetornoDatetimeLocal, transporteFimExclusivoDia, transporteInicioDia, transporteRetornoPrevisto } from './horario-sao-paulo';

describe('horario-sao-paulo', () => {
  it('formata instante em São Paulo', () => {
    expect(formatarInstanteSp('2026-11-23T13:00:00.000Z')).toMatch(/23\/11\/2026 · 10:00/);
  });

  it('gera offset explícito para filtros', () => {
    expect(transporteInicioDia('2026-11-23')).toMatch(/^2026-11-23T00:00:00[+-]/);
    expect(transporteFimExclusivoDia('2026-11-23')).toMatch(/^2026-11-24T00:00:00[+-]/);
  });

  it('converte retorno previsto com offset explícito de São Paulo', () => {
    const iso = transporteRetornoPrevisto('2026-11-25T10:00');
    expect(iso).toMatch(/^2026-11-25T10:00:00[+-]\d{2}:\d{2}$/);
    expect(datetimeLocalSaoPaulo(iso)).toBe('2026-11-25T10:00');
  });

  it('preserva meia-noite sem convertê-la para hora 24', () => {
    expect(transporteRetornoPrevisto('2026-11-25T00:00')).toBe('2026-11-25T00:00:00-03:00');
  });

  it('rejeita horário histórico inexistente ou ambíguo sem desambiguação', () => {
    expect(() => transporteRetornoPrevisto('2018-11-04T00:30')).toThrow();
    expect(() => transporteRetornoPrevisto('2019-02-16T23:30')).toThrow();
    expect(() => transporteRetornoPrevisto('2026-02-30T12:00')).toThrow();
  });

  it('sugere 48 horas transcorridas sem descontar o offset duas vezes', () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-11-24T13:00:00Z'));
    try {
      expect(sugestaoRetornoDatetimeLocal()).toBe('2026-11-26T10:00');
    } finally {
      vi.restoreAllMocks();
    }
  });
});
