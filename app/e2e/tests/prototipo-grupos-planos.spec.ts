import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { expect, test } from '@playwright/test';

test('protótipo demonstra retirada e nova configuração por modalidade com outro ID', async ({ page }) => {
  const arquivo = path.resolve(__dirname, '../../../openspec/changes/fase-2-cadastros/prototypes/grupos-planos.html');
  await page.goto(pathToFileURL(arquivo).href);
  await page.getByRole('tab', { name: 'Planos', exact: true }).click();
  const painel = page.locator('#plansPanel');
  await painel.getByRole('button', { name: 'Excluir plano Livre', exact: true }).click();
  await expect(painel).toContainText('Plano PLN-1044 retirado de uso');
  await painel.getByRole('button', { name: 'Configurar plano Livre', exact: true }).click();
  await expect(page.locator('#kmField')).not.toBeVisible();
  await expect(page.locator('#allowanceField')).not.toBeVisible();
  await page.locator('#daily').fill('230');
  await page.getByRole('button', { name: 'Salvar plano', exact: true }).click();
  await expect(painel.getByRole('status')).toContainText('Plano PLN-3001 salvo');
  await expect(painel.getByRole('status')).toContainText('IDs anteriores preservados: PLN-1044');
  await painel.getByRole('button', { name: 'Editar plano Controlado', exact: true }).click();
  await expect(page.locator('#allowanceField')).toBeVisible();
  await expect(page.locator('#kmField label')).toHaveText('Preço por km excedente');
  await page.locator('#daily').fill('-1');
  await page.getByRole('button', { name: 'Salvar plano', exact: true }).click();
  await expect(page.locator('#errorSummary')).toBeFocused();
});
