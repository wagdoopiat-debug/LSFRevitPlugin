using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes da viga trelicada: catalogo extensivel, geometria pura dos paineis (banzos,
    /// diagonais e montantes), parametros, altura efetiva e persistencia.
    /// </summary>
    public class LajeTrelicaTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        private static int Contar(List<MembroTrelica> membros, MembroTrelicaTipo tipo)
        {
            return membros.Count(m => m.Tipo == tipo);
        }

        // ---------- Catalogo ----------

        [Fact]
        public void Catalogo_de_trelica_oferece_os_tipos_implementados()
        {
            Assert.True(CatalogoTrelica.Implementado(TrelicaTipo.Warren));
            Assert.True(CatalogoTrelica.Implementado(TrelicaTipo.Pratt));
            Assert.True(CatalogoTrelica.Implementado(TrelicaTipo.Howe));
            Assert.True(CatalogoTrelica.Implementado(TrelicaTipo.DiagonaisAlternadas));
            Assert.True(CatalogoTrelica.Implementado(TrelicaTipo.MontantesVerticais));
        }

        [Fact]
        public void Catalogo_de_tipos_de_viga_agora_marca_trelica_como_implementada()
        {
            CatalogoTiposVigaLaje.TipoVigaInfo info =
                CatalogoTiposVigaLaje.Todos.FirstOrDefault(t => t.Tipo == TipoVigaLaje.Trelica);

            Assert.NotNull(info);
            Assert.True(info.Implementado);
        }

        // ---------- Numero de paineis ----------

        [Fact]
        public void Numero_de_paineis_deriva_do_vao_e_do_alvo()
        {
            Assert.Equal(10, ComposicaoTrelica.NumeroPaineis(6000.0, 600.0));
            Assert.Equal(15, ComposicaoTrelica.NumeroPaineis(6000.0, 400.0));
        }

        [Fact]
        public void Numero_de_paineis_respeita_os_limites()
        {
            Assert.Equal(ComposicaoTrelica.NumeroPaineisMinimo, ComposicaoTrelica.NumeroPaineis(100.0, 600.0));
            Assert.Equal(ComposicaoTrelica.NumeroPaineisMaximo, ComposicaoTrelica.NumeroPaineis(1e9, 600.0));
        }

        // ---------- Geometria dos membros ----------

        [Fact]
        public void Trelica_tem_dois_banzos_de_ponta_a_ponta()
        {
            List<MembroTrelica> membros =
                ComposicaoTrelica.Membros(TrelicaTipo.Warren, 6000.0, 200.0, 10);

            MembroTrelica sup = membros.First(m => m.Tipo == MembroTrelicaTipo.BanzoSuperior);
            MembroTrelica inf = membros.First(m => m.Tipo == MembroTrelicaTipo.BanzoInferior);

            Assert.Equal(0.0, sup.InicioUMm, 6);
            Assert.Equal(6000.0, sup.FimUMm, 6);
            Assert.Equal(100.0, sup.InicioZMm, 6);
            Assert.Equal(-100.0, inf.InicioZMm, 6);
            Assert.Equal(6000.0, sup.ComprimentoMm, 6);
        }

        [Fact]
        public void Warren_tem_uma_diagonal_por_painel_e_nenhum_montante()
        {
            List<MembroTrelica> membros =
                ComposicaoTrelica.Membros(TrelicaTipo.Warren, 6000.0, 200.0, 10);

            Assert.Equal(10, Contar(membros, MembroTrelicaTipo.Diagonal));
            Assert.Equal(0, Contar(membros, MembroTrelicaTipo.Montante));
            Assert.Equal(12, membros.Count);
        }

        [Fact]
        public void Montantes_verticais_tem_um_montante_por_no_interno()
        {
            List<MembroTrelica> membros =
                ComposicaoTrelica.Membros(TrelicaTipo.MontantesVerticais, 6000.0, 200.0, 10);

            Assert.Equal(9, Contar(membros, MembroTrelicaTipo.Montante));
            Assert.Equal(0, Contar(membros, MembroTrelicaTipo.Diagonal));
        }

        [Fact]
        public void Pratt_e_howe_miram_as_diagonais_em_sentidos_opostos()
        {
            List<MembroTrelica> pratt =
                ComposicaoTrelica.Membros(TrelicaTipo.Pratt, 6000.0, 200.0, 10);
            List<MembroTrelica> howe =
                ComposicaoTrelica.Membros(TrelicaTipo.Howe, 6000.0, 200.0, 10);

            List<MembroTrelica> dPratt = pratt.Where(m => m.Tipo == MembroTrelicaTipo.Diagonal).ToList();
            List<MembroTrelica> dHowe = howe.Where(m => m.Tipo == MembroTrelicaTipo.Diagonal).ToList();

            Assert.Equal(dPratt.Count, dHowe.Count);
            for (int i = 0; i < dPratt.Count; i++)
            {
                Assert.Equal(-dPratt[i].AnguloGraus, dHowe[i].AnguloGraus, 6);
            }
        }

        [Fact]
        public void Montante_vertical_e_de_fato_vertical()
        {
            List<MembroTrelica> membros =
                ComposicaoTrelica.Membros(TrelicaTipo.DiagonaisAlternadas, 6000.0, 200.0, 10);

            foreach (MembroTrelica m in membros.Where(x => x.Tipo == MembroTrelicaTipo.Montante))
            {
                Assert.Equal(90.0, System.Math.Abs(m.AnguloGraus), 6);
                Assert.Equal(200.0, m.ComprimentoMm, 6);
            }
        }

        [Fact]
        public void Vao_invalido_lanca()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ComposicaoTrelica.Membros(TrelicaTipo.Warren, 0.0, 200.0, 10));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ComposicaoTrelica.Membros(TrelicaTipo.Warren, 6000.0, 0.0, 10));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ComposicaoTrelica.Membros(TrelicaTipo.Warren, 6000.0, 200.0, 1));
        }

        // ---------- Parametros e validacao ----------

        [Fact]
        public void Parametros_de_trelica_validam_com_os_padroes()
        {
            var p = new ParametrosLaje { Contorno = Retangulo(), TipoViga = TipoVigaLaje.Trelica };

            Assert.Empty(p.Validar());
        }

        [Fact]
        public void Altura_de_trelica_invalida_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                TipoViga = TipoVigaLaje.Trelica,
                AlturaTrelicaMm = 0.0
            };

            Assert.Contains(p.Validar(), e => e.Contains("Altura da trelica"));
        }

        [Fact]
        public void Numero_de_paineis_de_trelica_abaixo_do_minimo_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                TipoViga = TipoVigaLaje.Trelica,
                NumeroPaineisTrelica = 1
            };

            Assert.Contains(p.Validar(), e => e.Contains("paineis da trelica"));
        }

        [Fact]
        public void Altura_efetiva_e_a_altura_da_trelica_quando_trelica()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                TipoViga = TipoVigaLaje.Trelica,
                AlturaTrelicaMm = 250.0
            };

            Assert.Equal(250.0, p.AlturaVigaEfetivaMm(), 6);
        }

        [Fact]
        public void Altura_efetiva_e_a_alma_do_perfil_quando_perfil_simples()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            var p = new ParametrosLaje { Contorno = Retangulo(), TipoViga = TipoVigaLaje.PerfilSimples, Perfil = perfil };

            Assert.Equal(perfil.LarguraAlmaMm, p.AlturaVigaEfetivaMm(), 6);
        }

        // ---------- Envelope fisico da trelica ----------

        [Theory]
        [InlineData(TrelicaTipo.Warren, MontagemTrelica.Encaixada)]
        [InlineData(TrelicaTipo.Warren, MontagemTrelica.FaceComFace)]
        [InlineData(TrelicaTipo.Pratt, MontagemTrelica.Encaixada)]
        [InlineData(TrelicaTipo.Pratt, MontagemTrelica.FaceComFace)]
        [InlineData(TrelicaTipo.Howe, MontagemTrelica.Encaixada)]
        [InlineData(TrelicaTipo.Howe, MontagemTrelica.FaceComFace)]
        [InlineData(TrelicaTipo.DiagonaisAlternadas, MontagemTrelica.Encaixada)]
        [InlineData(TrelicaTipo.DiagonaisAlternadas, MontagemTrelica.FaceComFace)]
        [InlineData(TrelicaTipo.MontantesVerticais, MontagemTrelica.Encaixada)]
        [InlineData(TrelicaTipo.MontantesVerticais, MontagemTrelica.FaceComFace)]
        public void Envelope_fisico_e_simetrico_e_maior_que_altura_entre_eixos(
            TrelicaTipo tipo, MontagemTrelica montagem)
        {
            const double alturaEntreEixos = 300.0;

            var envelope = ComposicaoTrelica.CalcularEnvelopeVertical(
                tipo, 6000.0, alturaEntreEixos, 10, montagem, 40.0, 90.0);

            double alturaFisica = envelope.TopoMm - envelope.BaseMm;
            double centro = (envelope.TopoMm + envelope.BaseMm) / 2.0;

            Assert.True(envelope.TopoMm > alturaEntreEixos / 2.0);
            Assert.True(envelope.BaseMm < -alturaEntreEixos / 2.0);
            Assert.True(alturaFisica > alturaEntreEixos);
            Assert.Equal(0.0, centro, 6);
        }

        [Theory]
        [InlineData(MontagemTrelica.Encaixada, 20.0)]
        [InlineData(MontagemTrelica.FaceComFace, 45.0)]
        public void Envelope_considera_projecao_vertical_dos_banzos(
            MontagemTrelica montagem, double projecaoMinima)
        {
            var envelope = ComposicaoTrelica.CalcularEnvelopeVertical(
                TrelicaTipo.Warren, 6000.0, 300.0, 10, montagem, 40.0, 90.0);

            Assert.True(envelope.TopoMm >= 150.0 + projecaoMinima - 1e-6);
            Assert.True(envelope.BaseMm <= -150.0 - projecaoMinima + 1e-6);
        }

        [Fact]
        public void Envelope_rejeita_dimensoes_de_secao_invalidas()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ComposicaoTrelica.CalcularEnvelopeVertical(
                    TrelicaTipo.Warren, 6000.0, 300.0, 10,
                    MontagemTrelica.Encaixada, 0.0, 90.0));

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ComposicaoTrelica.CalcularEnvelopeVertical(
                    TrelicaTipo.Warren, 6000.0, 300.0, 10,
                    MontagemTrelica.Encaixada, 40.0, double.NaN));
        }
        // ---------- Persistencia ----------

        [Fact]
        public void Persistencia_preserva_parametros_de_trelica()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                TipoViga = TipoVigaLaje.Trelica,
                Trelica = TrelicaTipo.Howe,
                AlturaTrelicaMm = 250.0,
                NumeroPaineisTrelica = 12,
                PainelTrelicaAlvoMm = 500.0
            };

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(
                ConfiguracaoLajeTexto.Serializar(p), out lido));

            Assert.Equal(TipoVigaLaje.Trelica, lido.TipoViga);
            Assert.Equal(TrelicaTipo.Howe, lido.Trelica);
            Assert.Equal(250.0, lido.AlturaTrelicaMm, 6);
            Assert.Equal(12, lido.NumeroPaineisTrelica);
            Assert.Equal(500.0, lido.PainelTrelicaAlvoMm, 6);
        }

        [Fact]
        public void Texto_antigo_sem_trelica_usa_padroes_seguros()
        {
            string texto = "versao=1\ntipo=Seca\ntipoViga=Trelica\ncontorno=0,0;6000,0;6000,4000;0,4000";

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));

            Assert.Equal(TrelicaTipo.Warren, lido.Trelica);
            Assert.Equal(ComposicaoTrelica.AlturaPadraoMm, lido.AlturaTrelicaMm, 6);
            Assert.Null(lido.NumeroPaineisTrelica);
        }
    }
}
