import { expect, test } from '@playwright/test';
import { abrirDevolucao, api, prepararDevolucao, senha } from '../suporte/devolucao';
import { randomUUID } from 'node:crypto';

test('devolução incerta mantém intenção, congela teclado e recupera mesma chave após reautenticar', async ({ page, request }) => {
  test.setTimeout(180_000);
  const dados = await prepararDevolucao(request);
  await abrirDevolucao(page, dados);
  const tentativas: Array<{ chave: string; etag: string; corpo: string | null }> = [];
  let liberarResposta!: () => void;
  const respostaRetida = new Promise<void>(resolve => liberarResposta = resolve);
  await page.route(`${api}/alugueis/${dados.contrato.id}/devolucao`, async route => {
    const r = route.request();
    tentativas.push({ chave: r.headers()['idempotency-key'], etag: r.headers()['if-match'], corpo: r.postData() });
    if (tentativas.length === 1) {
      const resposta = await route.fetch();
      expect(resposta.status()).toBe(200);
      await respostaRetida;
      return route.abort('connectionfailed');
    }
    if (tentativas.length === 2) return route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ code: 'sessao_invalida', status: 401 }) });
    return route.continue();
  });
  await page.getByRole('button', { name: 'Confirmar devolução', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Confirmando devolução…', exact: true })).toBeDisabled();
  await expect(page.getByLabel('Hodômetro final (km)', { exact: true })).toBeDisabled();
  await expect(page.getByLabel('Litros restantes no tanque', { exact: true })).toBeDisabled();
  liberarResposta();
  await expect(page.getByText('A devolução pode ter sido registrada.', { exact: true })).toBeVisible();
  for (const nome of ['Data e hora real da devolução', 'Hodômetro final (km)', 'Litros restantes no tanque'])
    await expect(page.getByLabel(nome, { exact: true })).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: /Apoio de viagem sintético/ })).toBeDisabled();
  await page.getByRole('link', { name: 'Veículos', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(new RegExp(`/alugueis/${dados.contrato.id}/devolucao$`));
  // Handler nativo bloqueia reload/fechamento; o teste não perde a intenção nem a sessão.
  expect(await page.evaluate(() => {
    const evento = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(evento);
    return evento.defaultPrevented;
  })).toBe(true);
  await page.getByRole('button', { name: 'Verificar devolução', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Reautenticar', exact: true })).toBeVisible();
  await page.getByLabel('Usuário', { exact: true }).fill(dados.usuario);
  await page.getByLabel('Senha', { exact: true }).fill(senha);
  await page.getByRole('button', { name: 'Reautenticar', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Reautenticar', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Verificar devolução', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('#sucesso-devolucao')).toContainText('Locação concluída');
  expect(tentativas).toHaveLength(3);
  expect(tentativas[1]).toEqual(tentativas[0]);
  expect(tentativas[2]).toEqual(tentativas[0]);
  expect(await page.evaluate(() => ({ local: localStorage.length, sessao: sessionStorage.length }))).toEqual({ local: 0, sessao: 0 });
  const consulta = await request.get(`${api}/alugueis/${dados.contrato.id}`, { headers: dados.headers });
  expect((await consulta.json()).devolucao.calculo.totalFinal).toBe('334.00');
});

for (const largura of [1440, 390]) {
  test(`J-04 devolução real CA-08, final e nova ocupação por teclado em ${largura}px`, async ({ page, request }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: largura, height: 900 });
    const dados = await prepararDevolucao(request);
    await abrirDevolucao(page, dados);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: `/tmp/opencode/f4-previa-${largura}.png`, fullPage: true });
    await page.getByRole('button', { name: 'Confirmar devolução', exact: true }).focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('#sucesso-devolucao')).toBeFocused();
    await expect(page.getByRole('heading', { name: 'Devolução concluída', exact: true })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Demonstrativo da devolução', exact: true })).toContainText('334,00');
    await expect(page.getByRole('region', { name: 'Demonstrativo da devolução', exact: true })).toContainText('1.000,00');
    await expect(page.getByRole('button', { name: 'Confirmar devolução', exact: true })).toHaveCount(0);
    await expect(page.getByLabel('Hodômetro final (km)', { exact: true })).toHaveCount(0);
    await page.screenshot({ path: `/tmp/opencode/f4-final-${largura}.png`, fullPage: true });
    await page.getByRole('button', { name: 'Consultar contrato', exact: true }).focus();
    await page.keyboard.press('Enter');
    const dialogo = page.getByRole('dialog');
    await expect(dialogo).toContainText('Final imutável');
    await expect(dialogo).toContainText('334,00');
    await expect(dialogo).toContainText('18570 km');
    await expect(dialogo).not.toContainText('52998224725');
    await expect(dialogo.getByRole('button', { name: /Editar|devolução|Reabrir/ })).toHaveCount(0);
    await page.getByRole('button', { name: 'Fechar consulta', exact: true }).focus();
    await page.keyboard.press('Shift+Tab');
    await expect(dialogo.getByRole('button', { name: 'Fechar', exact: true })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: 'Consultar contrato', exact: true })).toBeFocused();
    if (largura === 1440) {
      await page.setViewportSize({ width: 720, height: 900 });
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      await page.screenshot({ path: '/tmp/opencode/f4-final-reflow-200.png', fullPage: true });
      await page.setViewportSize({ width: largura, height: 900 });
    }
    const disponiveis = await request.get(`${api}/veiculos?somenteDisponiveis=true`, { headers: dados.headers });
    expect((await disponiveis.json()).items.some((v: { id: string }) => v.id === dados.veiculo.id)).toBe(true);
    const novo = await request.post(`${api}/alugueis`, { headers: { ...dados.headers, 'Idempotency-Key': randomUUID() }, data: dados.comando });
    expect(novo.status()).toBe(201);
    const novaLocacao = await novo.json();
    const anterior = await request.get(`${api}/alugueis/${dados.contrato.id}`, { headers: dados.headers });
    expect((await anterior.json()).devolucao.calculo.totalFinal).toBe('334.00');
    const ocupados = await request.get(`${api}/veiculos?somenteDisponiveis=true`, { headers: dados.headers });
    expect((await ocupados.json()).items.some((v: { id: string }) => v.id === dados.veiculo.id)).toBe(false);
    // Segunda contratação devolvida um minuto depois do previsto: D continua 2, multa única 20.
    await page.getByRole('link', { name: /Voltar para aluguéis/ }).click();
    await page.getByRole('button', { name: 'Registrar devolução', exact: true }).click();
    const atraso = new Date(Date.parse(dados.comando.retornoPrevisto) + 60_000);
    const local = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(atraso).replace(' ', 'T');
    await page.getByLabel('Data e hora real da devolução', { exact: true }).fill(local);
    await page.getByLabel('Hodômetro final (km)', { exact: true }).fill('18570');
    await page.getByLabel('Litros restantes no tanque', { exact: true }).fill('40');
    await page.getByRole('checkbox', { name: /Apoio de viagem sintético/ }).check();
    await page.getByRole('button', { name: 'Calcular prévia', exact: true }).click();
    await expect(page.getByLabel('Demonstrativo da devolução')).toContainText('354,00');
    await page.getByRole('button', { name: 'Confirmar devolução', exact: true }).click();
    await expect(page.locator('#sucesso-devolucao')).toBeVisible();
    const fechado = await request.get(`${api}/alugueis/${novaLocacao.id}`, { headers: dados.headers });
    expect((await fechado.json()).devolucao.calculo.totalFinal).toBe('354.00');
  });
}
