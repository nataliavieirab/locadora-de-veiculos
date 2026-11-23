import { expect, test, type Locator, type Page } from '@playwright/test';

const api = 'http://127.0.0.1:5285/api/v1';

test('pessoas: clientes PF/PJ, próprio condutor, vínculos e bloqueio por teclado', async ({ page, request }) => {
  test.setTimeout(180_000);
  const usuario = `pessoas${Date.now().toString(36)}`;
  const senha = 'abcdefgh';
  const registro = await request.post(`${api}/registro`, {
    data: { username: usuario, email: `${usuario}@example.invalid`, password: senha },
  });
  expect(registro.status()).toBe(201);
  await entrarPorTeclado(page, usuario, senha);

  await tabAte(page, page.getByRole('link', { name: 'Clientes e condutores', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Clientes e condutores', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Nenhum cliente cadastrado' })).toBeVisible();

  const dialogo = page.getByRole('dialog');
  await tabAte(page, page.getByRole('button', { name: '＋ Novo cliente', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).toBeVisible();
  await expect(page.getByLabel('Nome', { exact: true })).toBeFocused();
  await expect(page.getByLabel('CPF', { exact: true })).toBeVisible();
  await expect(page.getByLabel('RG', { exact: true })).toBeVisible();
  await expect(page.getByLabel('CNH', { exact: true })).toBeVisible();

  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar cliente', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-editor-pessoas')).toBeFocused();
  await expect(page.getByLabel('Nome', { exact: true })).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByLabel('CPF', { exact: true })).toHaveAttribute('aria-invalid', 'true');

  await tabAte(page, page.getByLabel('Nome', { exact: true }));
  await page.keyboard.type('Lia Monteiro');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('CPF', { exact: true })).toBeFocused();
  await page.keyboard.type('529.982.247-25');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('RG', { exact: true })).toBeFocused();
  await page.keyboard.type('RG-1');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('CNH', { exact: true })).toBeFocused();
  await page.keyboard.type('CNH-418');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Telefone', { exact: true })).toBeFocused();
  await page.keyboard.type('(11) 90000-1842');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('E-mail (opcional)', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Estado', { exact: true })).toBeFocused();
  await page.keyboard.type('SP');
  await page.keyboard.press('Tab');
  await page.keyboard.type('São Paulo');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Centro');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Rua A');
  await page.keyboard.press('Tab');
  await page.keyboard.type('10');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar cliente', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  const linhaLia = page.getByRole('row').filter({ hasText: 'Lia Monteiro' });
  await expect(linhaLia).toContainText('***.982.***-**');
  await expect(linhaLia).toContainText('Sem e-mail');
  await expect(linhaLia).not.toContainText('52998224725');

  await tabAte(page, page.getByRole('tab', { name: 'Clientes', exact: true }));
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Condutores', exact: true })).toBeFocused();
  await expect(page.getByRole('tab', { name: 'Condutores', exact: true })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('heading', { name: 'Nenhum condutor cadastrado' })).toBeVisible();

  await tabAte(page, page.getByRole('button', { name: '＋ Novo condutor', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#cliente-condutor')).toBeFocused();
  await page.keyboard.press('End');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('checkbox', { name: /próprio condutor/ })).toBeFocused();
  await page.keyboard.press('Space');
  await expect(page.getByLabel('Nome', { exact: true })).toHaveValue('Lia Monteiro');
  await expect(page.getByLabel('CPF', { exact: true })).toHaveValue('529.982.247-25');
  await expect(page.getByLabel('CNH', { exact: true })).toHaveValue('CNH-418');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Nome', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('CPF', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Telefone', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('CNH', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Validade da CNH', { exact: true })).toBeFocused();
  await page.getByLabel('Validade da CNH', { exact: true }).fill('2028-09-18');
  await tabAte(page, page.getByLabel('E-mail', { exact: true }));
  await page.keyboard.type('lia.condutora@example.invalid');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar condutor', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  const linhaCondutor = page.getByRole('row').filter({ hasText: 'Lia Monteiro' });
  await expect(linhaCondutor).toContainText('*******418');
  await expect(linhaCondutor).toContainText('18/09/2028');

  await tabAte(page, page.getByRole('tab', { name: 'Condutores', exact: true }));
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByRole('tab', { name: 'Clientes', exact: true })).toBeFocused();
  await tabAte(page, page.getByRole('button', { name: 'Excluir Lia Monteiro', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.locator('#erro-acao-pessoas')).toBeFocused();
  await expect(page.locator('#erro-acao-pessoas')).toContainText('possui condutor ativo');
  await expect(page.getByRole('row').filter({ hasText: 'Lia Monteiro' })).toBeVisible();

  await tabAte(page, page.getByRole('button', { name: '＋ Novo cliente', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Nome', { exact: true })).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(page.getByRole('button', { name: 'Pessoa jurídica', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('CNPJ', { exact: true })).toBeVisible();
  await expect(page.getByLabel('RG', { exact: true })).toHaveCount(0);
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Nome', { exact: true })).toBeFocused();
  await page.keyboard.type('Horizonte Azul Ltda.');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('CNPJ', { exact: true })).toBeFocused();
  await page.keyboard.type('11.222.333/0001-81');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Telefone', { exact: true })).toBeFocused();
  await page.keyboard.type('(21) 90000-2764');
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('E-mail (opcional)', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('Estado', { exact: true })).toBeFocused();
  await page.keyboard.type('RJ');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Rio de Janeiro');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Centro');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Rua B');
  await page.keyboard.press('Tab');
  await page.keyboard.type('20');
  await tabAte(page, dialogo.getByRole('button', { name: 'Salvar cliente', exact: true }));
  await page.keyboard.press('Enter');
  await expect(dialogo).not.toBeVisible();
  const linhaPj = page.getByRole('row').filter({ hasText: 'Horizonte Azul Ltda.' });
  await expect(linhaPj).toContainText('**.***.333/****-**');

  await tabAte(page, page.getByRole('tab', { name: 'Clientes', exact: true }));
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('tab', { name: 'Vínculos PF–PJ', exact: true })).toBeFocused();
  await expect(page.locator('#adicionar-vinculo')).toBeVisible();
  await tabAte(page, page.locator('#adicionar-vinculo'));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Remover vínculo de Lia Monteiro', exact: true })).toBeVisible();
  await tabAte(page, page.getByRole('button', { name: 'Remover vínculo de Lia Monteiro', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Remover vínculo de Lia Monteiro', exact: true })).toHaveCount(0);
  await expect(page.getByText('Nenhuma pessoa física vinculada a esta pessoa jurídica.')).toBeVisible();

  await tabAte(page, page.getByRole('tab', { name: 'Vínculos PF–PJ', exact: true }));
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByRole('tab', { name: 'Condutores', exact: true })).toBeFocused();
  await tabAte(page, page.getByRole('button', { name: 'Excluir condutor Lia Monteiro', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Nenhum condutor cadastrado' })).toBeVisible();

  await tabAte(page, page.getByRole('tab', { name: 'Condutores', exact: true }));
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByRole('tab', { name: 'Clientes', exact: true })).toBeFocused();
  await tabAte(page, page.getByRole('button', { name: 'Excluir Lia Monteiro', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('row').filter({ hasText: 'Lia Monteiro' })).toHaveCount(0);
  await tabAte(page, page.getByRole('button', { name: 'Excluir Horizonte Azul Ltda.', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'Nenhum cliente cadastrado' })).toBeVisible();
});

async function entrarPorTeclado(page: Page, usuario: string, senha: string): Promise<void> {
  await page.goto('/entrar');
  await tabAte(page, page.locator('#usuario'));
  await page.keyboard.type(usuario);
  await page.keyboard.press('Tab');
  await expect(page.locator('#senha')).toBeFocused();
  await page.keyboard.type(senha);
  await tabAte(page, page.getByRole('button', { name: 'Entrar', exact: true }));
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/painel$/);
}

async function tabAte(page: Page, destino: Locator): Promise<void> {
  await expect(destino).toBeVisible();
  for (let indice = 0; indice < 100; indice++) {
    if (await destino.evaluate((elemento) => elemento === document.activeElement)) {
      await expect(destino).toBeFocused();
      return;
    }
    await page.keyboard.press('Tab');
  }
  throw new Error(`Destino não alcançado por Tab: ${destino}`);
}
