import { Component, inject, signal } from '@angular/core';
import { form, FormField, FormRoot, required } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { LucideLogIn } from '@lucide/angular';
import { MENSAGEM_REVOGACAO } from '../../core/configuracao';
import { SessaoService } from '../../core/sessao.service';
import { ResumoErroComponent } from '../../shared/resumo-erro.component';
import { focarResumo, mensagemCampo } from './acesso-formulario';

@Component({
  selector: 'app-login',
  imports: [FormField, FormRoot, RouterLink, LucideLogIn, ResumoErroComponent],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly sessao = inject(SessaoService);
  private readonly roteador = inject(Router);
  protected readonly memoria = this.sessao.memoria;
  protected readonly revogacao = MENSAGEM_REVOGACAO;
  protected readonly mensagemCampo = mensagemCampo;
  protected readonly modelo = signal({ username: '', password: '' });
  protected readonly formulario = form(
    this.modelo,
    (caminho) => {
      required(caminho.username, { message: 'Informe o usuário.' });
      required(caminho.password, { message: 'Informe a senha.' });
    },
    {
      submission: {
        action: async () => {
          const atual = this.modelo();
          const erro = await this.sessao.entrar(atual.username, atual.password);
          if (erro) {
            await focarResumo();
            return;
          }

          await this.roteador.navigateByUrl('/painel');
        },
      },
    },
  );
}
