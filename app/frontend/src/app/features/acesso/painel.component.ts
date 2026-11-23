import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LucideLogOut } from '@lucide/angular';
import { SessaoService } from '../../core/sessao.service';

@Component({
  selector: 'app-painel',
  imports: [LucideLogOut, RouterLink],
  templateUrl: './painel.component.html',
})
export class PainelComponent implements OnInit {
  private readonly sessao = inject(SessaoService);
  private readonly roteador = inject(Router);
  protected readonly memoria = this.sessao.memoria;
  protected readonly falha = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.sessao.consultar();
    } catch {
      this.memoria.limparCredenciais();
      await this.roteador.navigateByUrl('/entrar');
    }
  }

  protected async sair(): Promise<void> {
    await this.sessao.sair();
    await this.roteador.navigateByUrl('/entrar');
  }
}
