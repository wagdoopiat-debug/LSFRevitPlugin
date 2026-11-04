using System;
using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
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
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(membro: banzo, montagem: MontagemTrelica.FaceComFace);

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
                foreach (MontagemTrelica montagem in new[] { MontagemTrelica.Encaixada, MontagemTrelica.FaceComFace })
                {
                    OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, montagem);
                    Vetor3D dE = SecaoMembroTrelica.DirecaoLocal(m);

                    // Encaixada: alma transversal. Face com Face: alma no plano.
                    double componenteNormal = montagem == MontagemTrelica.Encaixada ? 1.0 : 0.0;
                    Assert.Equal(componenteNormal, o.DirecaoSecaoPerp.ProdutoEscalar(Normal), 6);
                    Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(dE), 6);
                }
            }
        }

        [Fact]
        public void Diagonais_tambem_tem_a_alma_no_plano_da_trelica()
        {
            foreach (MembroTrelica m in Warren().Where(x => x.Tipo == MembroTrelicaTipo.Diagonal))
            {
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, MontagemTrelica.FaceComFace);
                Vetor3D dE = SecaoMembroTrelica.DirecaoLocal(m);

                Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(Normal), 6);
                Assert.Equal(0.0, o.DirecaoSecaoPerp.ProdutoEscalar(dE), 6);
                Assert.Equal(1.0, o.DirecaoSecaoPerp.Comprimento, 6);
            }
        }

        [Fact]
        public void Face_com_face_mantem_dV_uniforme_e_inverte_abertura_dos_membros_de_alma()
        {
            foreach (MembroTrelica m in Warren())
            {
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, MontagemTrelica.FaceComFace);
                Vetor3D dE = SecaoMembroTrelica.DirecaoLocal(m);
                Vetor3D dV = dE.ProdutoVetorial(o.DirecaoSecaoPerp);

                AssertVetor(new Vetor3D(0.0, -1.0, 0.0), dV);

                bool banzo = m.Tipo == MembroTrelicaTipo.BanzoSuperior
                    || m.Tipo == MembroTrelicaTipo.BanzoInferior;
                Assert.Equal(banzo, o.AbertoParaFrente);
            }
        }

        [Fact]
        public void Face_com_face_alinha_a_alma_real_ao_plano_da_trelica()
        {
            const double abaMm = 40.0;

            foreach (MembroTrelica m in Warren().Concat(ComMontantes()))
            {
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, MontagemTrelica.FaceComFace);
                double desloc = SecaoMembroTrelica.DeslocamentoAlmaMm(
                    MontagemTrelica.FaceComFace, o.AbertoParaFrente, abaMm);

                // A alma esta na borda do "C": U = -Aba/2 com abertura para +U, +Aba/2 com
                // abertura para -U. O deslocamento deve levar essa borda exatamente ao plano.
                double posicaoDaAlma = desloc
                    + (o.AbertoParaFrente ? -abaMm / 2.0 : abaMm / 2.0);

                Assert.Equal(0.0, posicaoDaAlma, 6);
                Assert.Equal(abaMm / 2.0, Math.Abs(desloc), 6);
            }
        }

        [Fact]
        public void Face_com_face_tem_abas_para_lados_opostos_entre_banzos_e_membros_de_alma()
        {
            const double abaMm = 40.0;

            foreach (MembroTrelica m in Warren().Concat(ComMontantes()))
            {
                bool banzo = m.Tipo == MembroTrelicaTipo.BanzoSuperior
                    || m.Tipo == MembroTrelicaTipo.BanzoInferior;
                OrientacaoSecaoMembro o = SecaoMembroTrelica.Calcular(m, MontagemTrelica.FaceComFace);
                double desloc = SecaoMembroTrelica.DeslocamentoAlmaMm(
                    MontagemTrelica.FaceComFace, o.AbertoParaFrente, abaMm);

                // O corpo do "C" fica do lado da abertura: banzos de um lado do plano e
                // montantes/diagonais do outro, com as almas em contato face a face.
                Assert.Equal(banzo ? 1 : -1, Math.Sign(desloc));
            }
        }

        [Fact]
        public void Encaixada_nao_desloca_a_alma_do_eixo()
        {
            Assert.Equal(0.0, SecaoMembroTrelica.DeslocamentoAlmaMm(
                MontagemTrelica.Encaixada, abertoParaFrente: true, abaMm: 40.0), 6);
            Assert.Equal(0.0, SecaoMembroTrelica.DeslocamentoAlmaMm(
                MontagemTrelica.Encaixada, abertoParaFrente: false, abaMm: 40.0), 6);
        }

        [Fact]
        public void Face_com_face_usa_um_unico_perfil_por_barra_sem_offset_de_separacao()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            List<SubPerfilViga> subs =
                ComposicaoViga.SubPerfisMontagem(MontagemTrelica.FaceComFace, perfil);

            Assert.Single(subs);
            Assert.Equal(0.0, subs[0].DeslocamentoTransversalMm, 6);
            Assert.False(subs[0].Espelhado);
        }

        [Fact]
        public void Membro_nulo_lanca()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SecaoMembroTrelica.Calcular(null, MontagemTrelica.Encaixada));
            Assert.Throws<System.ArgumentNullException>(
                () => SecaoMembroTrelica.DirecaoLocal(null));
        }

        // ---------- Orientacao das trelicas de extremidade ----------

        [Fact]
        public void Face_com_face_espelha_somente_a_borda_de_v_max()
        {
            Assert.Equal(1.0, SecaoMembroTrelica.SinalNormalPlanoTransversal(
                MontagemTrelica.FaceComFace, naBordaMin: true, naBordaMax: false), 6);
            Assert.Equal(-1.0, SecaoMembroTrelica.SinalNormalPlanoTransversal(
                MontagemTrelica.FaceComFace, naBordaMin: false, naBordaMax: true), 6);
        }

        [Fact]
        public void Encaixada_mantem_o_sentido_original_nas_duas_bordas()
        {
            Assert.Equal(1.0, SecaoMembroTrelica.SinalNormalPlanoTransversal(
                MontagemTrelica.Encaixada, naBordaMin: true, naBordaMax: false), 6);
            Assert.Equal(1.0, SecaoMembroTrelica.SinalNormalPlanoTransversal(
                MontagemTrelica.Encaixada, naBordaMin: false, naBordaMax: true), 6);
        }

        [Fact]
        public void Normal_espelhada_inverte_a_abertura_dos_banzos_face_com_face()
        {
            MembroTrelica banzo =
                new MembroTrelica(MembroTrelicaTipo.BanzoInferior, 0.0, -100.0, 6000.0, -100.0);
            var normalInvertida = new Vetor3D(0.0, -1.0, 0.0);

            OrientacaoSecaoMembro original =
                SecaoMembroTrelica.Calcular(banzo, MontagemTrelica.FaceComFace);
            OrientacaoSecaoMembro espelhada =
                SecaoMembroTrelica.Calcular(banzo, MontagemTrelica.FaceComFace, normalInvertida);

            AssertVetor(new Vetor3D(0.0, 0.0, 1.0), original.DirecaoSecaoPerp);
            AssertVetor(new Vetor3D(0.0, 0.0, -1.0), espelhada.DirecaoSecaoPerp);
            Assert.True(original.AbertoParaFrente);
            Assert.True(espelhada.AbertoParaFrente);
        }
    }
}
