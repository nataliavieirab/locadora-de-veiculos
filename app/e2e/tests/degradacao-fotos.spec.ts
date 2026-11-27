import { randomUUID } from 'node:crypto';
import { chmodSync, lstatSync } from 'node:fs';
import path from 'node:path';
import { expect, test } from '@playwright/test';
import { abrirDevolucao, api, prepararDevolucao } from '../suporte/devolucao';

test('gate F5-04/06: volume privado inacessível não impede cadastro, locação, devolução e histórico', async ({ page, request }, info) => {
  test.setTimeout(180_000);
  const raiz = info.config.metadata['fotosPrivadasRaiz'];
  expect(typeof raiz).toBe('string');
  expect(path.basename(raiz)).toMatch(/^e2e-fotos-[0-9a-f]+$/);
  const png = Buffer.from(await page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = 32; canvas.height = 24;
    const contexto = canvas.getContext('2d')!;
    contexto.fillStyle = '#2563eb'; contexto.fillRect(0, 0, 32, 24);
    return canvas.toDataURL('image/png').split(',')[1];
  }), 'base64');
  let pastaEmpresa: string | undefined;
  let referencia: string | undefined;
  try {
    const dados = await prepararDevolucao(request, async ({ headers, veiculo, sessao }) => {
      const detalhe = await request.get(`${api}/veiculos/${veiculo.id}`, { headers });
      const envio = await request.put(`${api}/veiculos/${veiculo.id}/foto`, {
        headers: { ...headers, 'If-Match': detalhe.headers()['etag'] },
        multipart: { arquivo: { name: 'sintetico.png', mimeType: 'image/png', buffer: png } },
      });
      expect(envio.status()).toBe(200);
      referencia = (await envio.json()).foto.id;
      const empresa = JSON.parse(Buffer.from(sessao.accessToken.split('.')[1], 'base64url').toString()).empresaId;
      expect(empresa).toMatch(/^[0-9a-f-]{36}$/i);
      const pasta = path.join(raiz, 'objetos', empresa.replaceAll('-', ''));
      expect(lstatSync(pasta).isSymbolicLink()).toBe(false);
      expect(lstatSync(pasta).isDirectory()).toBe(true);
      pastaEmpresa = pasta;
      // Falha real do volume por empresa. Preserva bytes; restauração obrigatória.
      chmodSync(pasta, 0o000);
      expect((await request.get(`${api}/veiculos/${veiculo.id}/foto`, { headers })).status()).toBe(503);
      const antes = await request.get(`${api}/veiculos/${veiculo.id}`, { headers });
      expect((await antes.json()).foto.id).toBe(referencia);
      const falha = await request.put(`${api}/veiculos/${veiculo.id}/foto`, {
        headers: { ...headers, 'If-Match': antes.headers()['etag'] },
        multipart: { arquivo: { name: 'sintetico.png', mimeType: 'image/png', buffer: png } },
      });
      expect(falha.status()).toBe(503);
      expect((await request.post(`${api}/veiculos`, { headers, data: {
        placa: 'FIC5A02', marca: 'Sintética', modelo: 'Sem foto', cor: 'Azul',
        ano: 2026, combustivel: 'Gasolina', capacidadeLitros: '50', grupoId: veiculo.grupoId,
      } })).status()).toBe(201);
      expect((await request.get(`${api}/veiculos`, { headers })).status()).toBe(200);
    });
    // O helper contratou por HTTP somente depois da indisponibilidade real.
    expect(dados.contrato.estado).toBe('EmAberto');
    expect((await request.get(`${api}/veiculos/${dados.veiculo.id}/foto`, { headers: dados.headers })).status()).toBe(503);
    await abrirDevolucao(page, dados);
    const corpo = { retornoReal: dados.comando.retornoPrevisto, hodometroFinal: 18570,
      litrosRestantes: '40', taxasAdicionaisIds: [dados.diaria.id] };
    const previa = await request.post(`${api}/alugueis/${dados.contrato.id}/devolucao/previa`, {
      headers: { ...dados.headers, 'If-Match': dados.etag }, data: corpo,
    });
    expect(previa.status()).toBe(200);
    const final = await request.post(`${api}/alugueis/${dados.contrato.id}/devolucao`, {
      headers: { ...dados.headers, 'If-Match': dados.etag, 'Idempotency-Key': randomUUID() },
      data: { ...corpo, versoesAceitas: (await previa.json()).versoesAceitas },
    });
    expect(final.status()).toBe(200);
    const demonstrativo = (await final.json()).devolucao;
    const consulta = await request.get(`${api}/alugueis/${dados.contrato.id}`, { headers: dados.headers });
    expect((await consulta.json()).devolucao).toEqual(demonstrativo);
    const oculto = await request.delete(`${api}/alugueis/${dados.contrato.id}`, {
      headers: { ...dados.headers, 'If-Match': consulta.headers()['etag'] },
    });
    expect(oculto.status()).toBe(204);
    const historico = await request.get(`${api}/alugueis/${dados.contrato.id}?incluirOcultos=true`, { headers: dados.headers });
    expect(historico.status()).toBe(200);
    expect((await historico.json()).devolucao).toEqual(demonstrativo);
    expect((await request.get(`${api}/veiculos/${dados.veiculo.id}/foto`, { headers: dados.headers })).status()).toBe(503);
    const preservado = await request.get(`${api}/veiculos/${dados.veiculo.id}`, { headers: dados.headers });
    expect((await preservado.json()).foto.id).toBe(referencia);
  } finally {
    if (pastaEmpresa) chmodSync(pastaEmpresa, 0o700);
  }
});
