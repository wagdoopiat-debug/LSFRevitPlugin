using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes da regra pura de orientacao da secao dos membros da trelica (Correcao 1/2):
    /// membros de alma com a alma no plano da trelica e BackToBack uniforme para fora do plano;
    /// banzos Encaixada como canal/guia com abertura para o interior.
    /// </summary>
    public class SecaoMembroTrelicaTests
    {
        private static readonly Vetor3D Normal = SecaoMembroTrelica.NormalLocal;

        private static List<MembroTrelica> Warren()
        {
            return ComposicaoTrelica.Membros(TrelicaTipo.Warren, 6000.0, 200.0, 10);
        }

        private static List<MembroTrelica> ComMontantes()
        {
            return ComposicaoTrelica.Membros(TrelicaTipo.MontantesVerticais, 6000.0, 200.0, 10);
        }

        private static void AssertVetor(Vetor3D esperado, Vetor3D atual)
        {
            Assert.Equal(esperado.X, atual.X, 6);
            Assert.Equal(esperado.Y, atual.Y, 6);
            Assert.Equal(esperado.Z, atual.Z, 6);
        }

        // ---------- Banzos ----------

        [Fact]
        public void Banzo_back_to_back_tem_alma_vertical()
        {
            List<MembroTrelica> membros = Warren();

            foreach (MembroTrelica banzo in membros.Where(m =>
                m.Tipo == MembroTrelicaTipo.BanzoSuperior || m.Tipo == MembroTrelicaTipo.BanzoInferior))
            {
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(membro: banzo, montagem: MontagemTrelica.BackToBack);

                // dS = dE x normal; banzo e horizontal (dE = +x) => vertical (+z).
                AssertVetor(new Vetor3D(0.0, 0.0, 1.0), o.DirecaoSecaoPerp);
                Assert.True(o.AbertoParaFrente);
            }
        }

        [Fact]
        public void Banzo_encaixado_e_canal_com_abertura_para_o_interior()
        {
            MembroTrelica inferior =
                new MembroTrelica(MembroTrelicaTipo.BanzoInferior, 0.0, -100.0, 6000.0, -100.0);
            MembroTrelica superior = new MembroTrelica(MembroTrelicaTipo.BanzoSuperior, 0.0, 100.0, 6000.0, 100.0);

            OrientacaoSecaoMembro inf = SecaoMembroTrelica.Calcular(inferior, MontagemTrelica.Encaixada);
            OrientacaoSecaoMembro sup = SecaoMembroTrelica.Calcular(superior, MontagemTrelica.Encaixada);

            // Alma horizontal (ao longo da normal), abas verticais.
            AssertVetor(Normal, inf.DirecaoSecaoPerp);
            AssertVetor(Normal, sup.DirecaoSecaoPerp);

            // Abertura para o interior: inferior abre para cima, superior para baixo.
            Assert.True(inf.AbertoParaFrente);
            Assert.False(sup.AbertoParaFrente);
        }

        [Fact]
        public void Banzo_encaixado_tem_dV_para_cima()
        {
            MembroTrelica inferior = new MembroTrelica(MembroTrelicaTipo.BanzoInferior, 0.0, -100.0, 6000.0, -100.0);
            OrientacaoSecaoMembro inf = SecaoMembroTrelica.Calcular(inferior, MontagemTrelica.Encaixada);

            // dE = u, dS = normal -> dV = dE x dS = (0,0,1): eixo U da secao apontando para cima,
            // logo abertoParaFrente=true significa abertura para cima.
            Vetor3D dV = SecaoMembroTrelica.DirecaoLocal(inferior).ProdutoVetorial(inf.DirecaoSecaoPerp);
            AssertVetor(new Vetor3D(0.0, 0.0, 1.0), dV);
        }

        [Fact]
        public void Membros_de_alma_tem_a_alma_no_plano_da_trelica()
        {
            foreach (MembroTrelica m in ComMontantes().Where(x => x.Tipo == MembroTrelicaTipo.Montante))
            {
                foreach (MontagemTrelica montagem in new[] { MontagemTrelica.Encaixada, MontagemTrelica.BackToBack })
                {
                    OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, montagem);
                    Vetor3D dE = SecaoMembroTrelica.DirecaoLocal(m);

                    // A alma nao tem componente fora do plano (dS perpendicular a normal) e e
                    // perpendicular ao eixo do membro.
                    Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(Normal), 6);
                    Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(dE), 6);
                }
            }
        }

        [Fact]
        public void Diagonais_tambem_tem_a_alma_no_plano_da_trelica()
        {
            foreach (MembroTrelica m in Warren().Where(x => x.Tipo == MembroTrelicaTipo.Diagonal))
            {
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, MontagemTrelica.BackToBack);
                Vetor3D dE = SecaoMembroTrelica.DirecaoLocal(m);

                Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(Normal), 6);
                Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(dE), 6);
                Assert.Equal(1.0, o.DirecaoSecaoPerp.Comprimento, 6);
            }
        }

        [Fact]
        public void BackToBack_tem_dV_uniforme_igual_a_menos_normal_em_todos_os_membros()
        {
            foreach (MembroTrelica m in Warren())
            {
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, MontagemTrelica.BackToBack);
                Vetor3D dE = SecaoMembroTrelica.DirecaoLocal(m);
                Vetor3D dV = dE.ProdutoVetorial(o.DirecaoSecaoPerp);

                AssertVetor(new Vetor3D(0.0, -1.0, 0.0), dV);
            }
        }

        [Fact]
        public void Membro_nulo_lanca()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SecaoMembroTrelica.Calcular(null, MontagemTrelica.Encaixada));
            Assert.Throws<System.ArgumentNullException>(
                () => SecaoMembroTrelica.DirecaoLocal(null));
        }
    }
}
