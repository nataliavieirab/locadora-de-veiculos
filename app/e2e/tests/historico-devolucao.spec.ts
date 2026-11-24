import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { api, prepararDevolucao, senha } from '../suporte/devolucao';

for (const largura of [1440, 390]) {
  test(`J-05 final e ocultação preservados, foco e resposta ${largura === 390 ? 'perdida' : 'confirmada'} em ${largura}px`, async ({ page, request }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: largura, height: 900 });
    const dados = await prepararDevolucao(request);
    const corpo = { retornoReal: dados.comando.retornoPrevisto, hodometroFinal: 18570,
      litrosRestantes: '40', taxasAdicionaisIds: [dados.diaria.id] };
    const previa = await request.post(`${api}/alugueis/${dados.contrato.id}/devolucao/previa`, {
      headers: { ...dados.headers, 'If-Match': dados.etag }, data: corpo,
    });
    expect(previa.status()).toBe(200);
    const fechado = await request.post(`${api}/alugueis/${dados.contrato.id}/devolucao`, {
      headers: { ...dados.headers, 'If-Match': dados.etag, 'Idempotency-Key': randomUUID() },
      data: { ...corpo, versoesAceitas: (await previa.json()).versoesAceitas },
    });
    expect(fechado.status()).toBe(200);
    await page.goto('/entrar');
    await page.getByLabel('Usuário', { exact: true }).fill(dados.usuario);
    await page.getByLabel('Senha', { exact: true }).fill(senha);
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await expect(page).toHaveURL(/\/painel$/);
    await page.getByRole('link', { name: 'Aluguéis', exact: true }).click();
    await page.getByLabel('Estado', { exact: true }).selectOption('Concluido');
    await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).focus();
    await page.keyboard.press('Enter');
    const linha = page.locator('tbody tr').filter({ hasText: dados.contrato.id });
    await expect(linha).toContainText('Concluído');
    await expect(linha).toContainText('334,00');
    await expect(linha).toContainText('Total final');
    await expect(linha.getByRole('button', { name: /Editar|Registrar devolução/ })).toHaveCount(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: `/tmp/opencode/f4-historico-${largura}.png`, fullPage: true });
    const consultar = linha.getByRole('button', { name: /Consultar contrato/ });
    await consultar.focus(); await page.keyboard.press('Enter');
    const drawer = page.getByRole('dialog');
    await expect(drawer).toContainText('Final imutável');
    await expect(drawer).toContainText('334,00');
    await expect(drawer).toContainText('1.000,00');
    await expect(drawer).not.toContainText('52998224725');
    await expect(drawer).not.toContainText('CNH-sintetica-418');
    await expect(drawer.getByRole('button', { name: /Editar|Registrar devolução|Restaurar/ })).toHaveCount(0);
    const rodape = await drawer.locator(':scope > footer').boundingBox();
    expect(rodape).not.toBeNull();
    expect(rodape!.y).toBeGreaterThanOrEqual(0);
    expect(rodape!.y + rodape!.height).toBeLessThanOrEqual(901);
    await page.screenshot({ path: `/tmp/opencode/f4-historico-final-${largura}.png` });
    await page.keyboard.press('Escape');
    await expect(consultar).toBeFocused();
    const ocultar = linha.getByRole('button', { name: 'Ocultar da operação', exact: true });
    await ocultar.focus(); await page.keyboard.press('Enter');
    const dialogo = page.getByRole('dialog', { name: 'Ocultar da operação?', exact: true });
    await expect(dialogo.getByRole('button', { name: 'Voltar sem ocultar', exact: true })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(ocultar).toBeFocused();
    await ocultar.press('Enter');
    await dialogo.getByRole('button', { name: 'Voltar sem ocultar', exact: true }).press('Enter');
    await expect(linha).toHaveCount(1);
    await expect(ocultar).toBeFocused();
    await consultar.press('Enter');
    await drawer.getByRole('button', { name: 'Ocultar da operação', exact: true }).press('Enter');
    await expect(dialogo.getByRole('button', { name: 'Voltar sem ocultar', exact: true })).toBeFocused();
    await expect(dialogo.getByRole('button', { name: 'Ocultar da operação', exact: true })).toBeEnabled();
    await page.keyboard.press('Shift+Tab');
    await expect(dialogo.getByRole('button', { name: 'Ocultar da operação', exact: true })).toBeFocused();
    let liberar!: () => void;
    const retida = new Promise<void>(r => liberar = r);
    await page.route(`${api}/alugueis/${dados.contrato.id}`, async route => {
      if (route.request().method() !== 'DELETE') return route.continue();
      const resposta = await route.fetch();
      expect(resposta.status()).toBe(204);
      await retida;
      return largura === 390 ? route.abort('connectionfailed') : route.fulfill({ response: resposta });
    });
    await page.keyboard.press('Enter');
    await expect(dialogo.getByRole('button', { name: 'Ocultando…', exact: true })).toBeDisabled();
    await page.keyboard.press('Escape');
    await expect(dialogo).toBeVisible();
    liberar();
    if (largura === 390) {
      await expect(dialogo).toContainText('Resultado da ocultação não confirmado');
      await expect(page.locator('#aviso-alugueis')).toHaveCount(0);
      await dialogo.getByRole('button', { name: 'Atualizar consulta', exact: true }).press('Enter');
    } else {
      await expect(page.locator('#aviso-alugueis')).toContainText('Locação ocultada');
    }
    await expect(linha).toHaveCount(0);
    await expect(page.locator('#titulo-alugueis')).toBeFocused();
    await page.getByRole('checkbox', { name: 'Incluir ocultos', exact: true }).focus();
    await page.keyboard.press('Space');
    await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).press('Enter');
    await expect(linha).toContainText('Oculto da operação');
    await expect(linha.getByRole('button', { name: 'Ocultar da operação', exact: true })).toHaveCount(0);
    await linha.getByRole('button', { name: /Consultar contrato/ }).press('Enter');
    await expect(drawer).toContainText('334,00');
    await expect(drawer).toContainText('Final imutável');
    await expect(drawer.getByRole('button', { name: /Ocultar|Restaurar|Editar|Registrar devolução/ })).toHaveCount(0);
    if (largura === 1440) {
      await page.setViewportSize({ width: 720, height: 900 });
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
      const rodapeEstreito = await drawer.locator(':scope > footer').boundingBox();
      expect(rodapeEstreito!.y + rodapeEstreito!.height).toBeLessThanOrEqual(901);
      await page.screenshot({ path: '/tmp/opencode/f4-historico-reflow-200.png' });
    }
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Limpar filtros', exact: true }).press('Enter');
    await expect(page.getByRole('checkbox', { name: 'Incluir ocultos', exact: true })).not.toBeChecked();
    await expect(linha).toHaveCount(0);
    expect((await request.get(`${api}/alugueis/${dados.contrato.id}`, { headers: dados.headers })).status()).toBe(404);
    const oculto = await request.get(`${api}/alugueis/${dados.contrato.id}?incluirOcultos=true`, { headers: dados.headers });
    expect((await oculto.json()).devolucao.id).toBe((await fechado.json()).devolucao.id);
  });
}
