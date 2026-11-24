/** Fuso operacional fixo (CA-01); independe do fuso do dispositivo na exibição. */
const FUSO = 'America/Sao_Paulo';

export function formatarInstanteSp(iso: string): string {
  const instante = new Date(iso);
  const data = instante.toLocaleDateString('pt-BR', { timeZone: FUSO, day: '2-digit', month: '2-digit', year: 'numeric' });
  const hora = instante.toLocaleTimeString('pt-BR', { timeZone: FUSO, hour: '2-digit', minute: '2-digit', hour12: false });
  return `${data} · ${hora}`;
}

/** Início do dia civil em São Paulo com offset explícito (filtro `saidaDe`). */
export function transporteInicioDia(dia: string): string {
  const partes = new Intl.DateTimeFormat('en-US', {
    timeZone: FUSO, timeZoneName: 'longOffset', year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(new Date(`${dia}T12:00:00Z`));
  const offset = partes.find((p) => p.type === 'timeZoneName')?.value?.replace('GMT', '') ?? '-03:00';
  return `${dia}T00:00:00${offset}`;
}

/** Limite exclusivo: início do dia seguinte ao informado (`saidaAte`). */
export function transporteFimExclusivoDia(dia: string): string {
  const [ano, mes, diaNum] = dia.split('-').map(Number);
  const proximo = new Date(Date.UTC(ano, mes - 1, diaNum + 1, 12));
  const amanha = proximo.toLocaleDateString('en-CA', { timeZone: FUSO });
  return transporteInicioDia(amanha);
}

function partesWallSp(epochMs: number): { y: number; mo: number; d: number; h: number; mi: number } {
  const partes = new Intl.DateTimeFormat('en-US', {
    timeZone: FUSO, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(new Date(epochMs));
  const ler = (tipo: string) => Number(partes.find((p) => p.type === tipo)?.value ?? '0');
  return { y: ler('year'), mo: ler('month'), d: ler('day'), h: ler('hour'), mi: ler('minute') };
}

function offsetSp(epochMs: number): string {
  const nome = new Intl.DateTimeFormat('en-US', { timeZone: FUSO, timeZoneName: 'longOffset' })
    .formatToParts(new Date(epochMs)).find((p) => p.type === 'timeZoneName')?.value ?? 'GMT-03:00';
  return nome.replace('GMT', '');
}

/** Valor para `datetime-local` a partir de ISO; o relógio exibido é o de São Paulo. */
export function datetimeLocalSaoPaulo(iso: string): string {
  const p = partesWallSp(Date.parse(iso));
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${p.y}-${pad(p.mo)}-${pad(p.d)}T${pad(p.h)}:${pad(p.mi)}`;
}

/** Converte `datetime-local` como horário civil de São Paulo para ISO 8601 com offset explícito. */
export function transporteRetornoPrevisto(local: string): string {
  const match = /^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})$/.exec(local.trim());
  if (!match) throw new Error('Informe data e hora de retorno.');
  const alvo = local.trim();
  const base = `${match[1]}T${match[2]}:${match[3]}:00`;
  const centro = Date.parse(`${base}Z`);
  const offsets = new Set([-36, 0, 36].map((horas) => offsetSp(centro + horas * 3_600_000)));
  const candidatos = [...offsets].map((offset) => `${base}${offset}`)
    .filter((iso) => Number.isFinite(Date.parse(iso)) && datetimeLocalSaoPaulo(iso) === alvo);
  if (candidatos.length !== 1) throw new Error('Horário inexistente ou ambíguo em São Paulo. Informe outro horário.');
  return candidatos[0];
}

/** Sugestão inicial de retorno (+48 h no relógio de São Paulo). */
export function sugestaoRetornoDatetimeLocal(): string {
  return datetimeLocalSaoPaulo(new Date(Date.now() + 48 * 3_600_000).toISOString());
}
