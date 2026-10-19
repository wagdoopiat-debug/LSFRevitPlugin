using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes do telhamento TP40 recortado pelo contorno (faixas de chapa e enchimento de ondas).
    /// </summary>
    public class LajeTelhamentoTests
    {
        private static TelhaTp40 Telha()
        {
            return TelhaTp40.CriarPadrao();
        }

        /// <summary>Retangulo de uma coluna avancada (com recobrimento lateral) x 600 mm em v.</summary>
        private static ContornoLaje RetanguloUmaColuna(TelhaTp40 telha)
        {
            // A passagem padrao cobre extensaoU = passoAvanco * 1 para ficar no primeiro passo.
            var passo = SecaoTelhaTp40.PassoAvanco(telha);
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(passo, 0),
                new Ponto2DMm(passo, 600), new Ponto2DMm(0, 600)
            });
        }

        private static ContornoLaje FormaL()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0), new Ponto2DMm(6000, 2000),
                new Ponto2DMm(2000, 2000), new Ponto2DMm(2000, 6000), new Ponto2DMm(0, 6000)
            });
        }

        [Fact]
        public void Perfil_inferior_termina_na_largura_util_e_atinge_a_altura_da_crista()
        {
            TelhaTp40 telha = Telha();

            List<Ponto2DMm> perfil = SecaoTelhaTp40.PerfilInferior(telha);

            Assert.True(perfil.Count >= 2);
            Assert.Equal(0.0, perfil[0].XMm, 6);
            Assert.Equal(telha.LarguraUtilMm.Valor, perfil[perfil.Count - 1].XMm, 6);
            Assert.Equal(telha.AlturaOndaMm.Valor, perfil.Max(p => p.YMm), 6);
            Assert.Equal(0.0, perfil.Min(p => p.YMm), 6);
        }

        [Fact]
        public void Telha_de_uma_coluna_cobre_a_largura_com_a_espessura_modelada()
        {
            TelhaTp40 telha = Telha();
            ContornoLaje contorno = RetanguloUmaColuna(telha);
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            ResultadoFaixas r = TelhamentoTp40.CalcularTelha(contorno, orientacao, telha, 0.43);

            Assert.NotEmpty(r.Faixas);
            Assert.All(r.Faixas, f => Assert.Equal(PapelFaixa.Telha, f.Papel));
            Assert.All(r.Faixas, f => Assert.True(f.ComprimentoMm > 0.0));

            double soma = r.Faixas.Sum(f => f.AreaMm2);
            double t = SecaoTelhaTp40.EspessuraModelada(telha, 0.43);
            double passo = SecaoTelhaTp40.PassoAvanco(telha);
            Assert.Equal(t * passo, soma, 3);
        }

        [Fact]
        public void Enchimento_equivale_a_espessura_equivalente_do_catalogo()
        {
            TelhaTp40 telha = Telha();
            ContornoLaje contorno = RetanguloUmaColuna(telha);
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            ResultadoFaixas r = TelhamentoTp40.CalcularEnchimento(contorno, orientacao, telha);

            Assert.NotEmpty(r.Faixas);
            Assert.All(r.Faixas, f => Assert.Equal(PapelFaixa.Enchimento, f.Papel));
            Assert.Empty(r.Avisos);

            double soma = r.Faixas.Sum(f => f.AreaMm2);
            double espessuraMm = soma / SecaoTelhaTp40.PassoAvanco(telha);
            double esperadoMm = telha.EnchimentoOndasM3PorM2.Valor * 1000.0;

            Assert.Equal(esperadoMm, espessuraMm, 3);
        }

        [Fact]
        public void Enchimento_nao_cobre_os_segmentos_de_crista()
        {
            TelhaTp40 telha = Telha();
            ContornoLaje contorno = RetanguloUmaColuna(telha);
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            List<Ponto2DMm> perfil = SecaoTelhaTp40.PerfilInferior(telha);
            double altura = telha.AlturaOndaMm.Valor;

            var crista = new List<int>();
            for (int seg = 0; seg < perfil.Count - 1; seg++)
            {
                if (System.Math.Abs(perfil[seg].YMm - altura) < 1e-6
                    && System.Math.Abs(perfil[seg + 1].YMm - altura) < 1e-6)
                {
                    crista.Add(seg);
                }
            }

            Assert.NotEmpty(crista);

            ResultadoFaixas r = TelhamentoTp40.CalcularEnchimento(contorno, orientacao, telha);

            Assert.DoesNotContain(r.Faixas, f => crista.Contains(f.Segmento));
        }

        [Fact]
        public void Contorno_retangular_nao_gera_aviso_de_recorte_lateral()
        {
            TelhaTp40 telha = Telha();
            ContornoLaje contorno = RetanguloUmaColuna(telha);
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            ResultadoFaixas r = TelhamentoTp40.CalcularTelha(contorno, orientacao, telha, 0.43);

            Assert.DoesNotContain(r.Avisos, a => a.Contains("retangular"));
            Assert.Equal(
                SecaoTelhaTp40.ContagemChapas(orientacao.ExtensaoUMm, orientacao.ExtensaoVMm, telha),
                r.ChapasTelha);
        }

        [Fact]
        public void Contorno_nao_retangular_gera_aviso_de_borda_serrilhada()
        {
            TelhaTp40 telha = Telha();
            ContornoLaje contorno = FormaL();
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, null);

            ResultadoFaixas r = TelhamentoTp40.CalcularTelha(contorno, orientacao, telha, 0.43);

            Assert.NotEmpty(r.Faixas);
            Assert.Contains(r.Avisos, a => a.Contains("retangular"));
        }

        // ---------- Correcao 4: encaixe lateral (side-lap) ----------

        [Fact]
        public void PassoAvanco_reduz_a_largura_util_pelo_recobrimento_de_duas_ondas()
        {
            TelhaTp40 telha = Telha();

            double passo = telha.PassoOndaMm.Valor; // 980 / 4 = 245
            Assert.Equal(245.0, passo, 6);
            Assert.Equal(telha.LarguraUtilMm.Valor - 2.0 * passo, SecaoTelhaTp40.PassoAvanco(telha), 6);
            Assert.Equal(2.0 * passo, SecaoTelhaTp40.RecobrimentoLateralOndaMm(telha), 6);
        }

        [Fact]
        public void Perfil_do_telhamento_e_continuo_sem_saltos_maiores_que_o_passo()
        {
            List<Ponto2DMm> perfil = SecaoTelhaTp40.PerfilInferior(Telha());
            double passo = Telha().PassoOndaMm.Valor;

            for (int i = 1; i < perfil.Count; i++)
            {
                double du = perfil[i].XMm - perfil[i - 1].XMm;
                Assert.True(du >= 0.0, "Perfil nao pode andar para tras em X.");
                Assert.True(du <= passo + 1e-6, "Salto no perfil maior que o passo da onda.");
            }
        }

        [Fact]
        public void Colunas_avancam_sem_lacunas_e_empilham_pela_espessura_na_sobreposicao()
        {
            TelhaTp40 telha = Telha();
            double avanco = SecaoTelhaTp40.PassoAvanco(telha);
            double t = SecaoTelhaTp40.EspessuraModelada(telha, 0.43);

            // Tres passos de avanco em u x 600 mm em v.
            var contorno = new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(3.0 * avanco, 0),
                new Ponto2DMm(3.0 * avanco, 600), new Ponto2DMm(0, 600)
            });
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            ResultadoFaixas r = TelhamentoTp40.CalcularTelha(contorno, orientacao, telha, 0.43);

            int colunas = r.Faixas.Select(f => f.Coluna).Distinct().Count();
            Assert.Equal(3, colunas);

            // Em cada par de colunas adjacentes, a base da coluna seguinte coincide com o topo da
            // anterior (empilhamento exato) -- sem lacuna e sem interpenetracao na sobreposicao.
            List<FaixaPerfil> base0 = r.Faixas.Where(f => f.Coluna == 0 && f.Segmento == 0).ToList();
            List<FaixaPerfil> base1 = r.Faixas.Where(f => f.Coluna == 1 && f.Segmento == 0).ToList();
            Assert.NotEmpty(base0);
            Assert.NotEmpty(base1);

            double topoCol0 = base0.Max(f => f.D.YMm);
            double fundoCol1 = base1.Min(f => f.A.YMm);
            Assert.Equal(topoCol0 + 0.0, fundoCol1, 6);
            Assert.Equal(t, fundoCol1 - base0.Min(f => f.A.YMm), 6);
        }

        [Fact]
        public void Avancos_acumulados_geram_aviso_quando_ultrapassam_o_espaco_acima_da_onda()
        {
            TelhaTp40 telha = Telha();
            double avanco = SecaoTelhaTp40.PassoAvanco(telha);

            // 40 colunas acumulam 40 * 1 mm de elevacao (acima do espaco previsto para argamassa).
            var contorno = new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(40.0 * avanco, 0),
                new Ponto2DMm(40.0 * avanco, 600), new Ponto2DMm(0, 600)
            });
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            ResultadoFaixas r = TelhamentoTp40.CalcularTelha(contorno, orientacao, telha, 0.43);

            Assert.Contains(r.Avisos, a => a.Contains("Empilhamento lateral acumulado"));
        }
    }
}
