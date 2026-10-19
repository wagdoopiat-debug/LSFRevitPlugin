using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes do perfil transversal puro da telha TP40 (geometria 3D da laje umida).
    /// </summary>
    public class LajeTelhaTests
    {
        [Fact]
        public void Perfil_da_telha_e_fechado_e_maior_que_um_retangulo()
        {
            List<Ponto2DMm> perfil = SecaoTelhaTp40.Contorno(TelhaTp40.CriarPadrao(), 0.43);

            Assert.True(perfil.Count > 8);
            Assert.True(perfil.Count % 2 == 0);
        }

        [Fact]
        public void Perfil_cobre_a_largura_util_modelada()
        {
            TelhaTp40 telha = TelhaTp40.CriarPadrao();
            List<Ponto2DMm> perfil = SecaoTelhaTp40.Contorno(telha, 0.43);

            double largura = perfil.Max(p => p.XMm);
            Assert.Equal(SecaoTelhaTp40.LarguraModeladaMm(telha), largura, 6);
        }

        [Fact]
        public void Perfil_tem_altura_da_onda_somada_a_espessura()
        {
            TelhaTp40 telha = TelhaTp40.CriarPadrao();
            List<Ponto2DMm> perfil = SecaoTelhaTp40.Contorno(telha, 0.5);

            double altura = perfil.Max(p => p.YMm);
            double esperado = telha.AlturaOndaMm.Valor + SecaoTelhaTp40.EspessuraMinimaModeladaMm;
            Assert.Equal(esperado, altura, 6);
        }

        [Fact]
        public void Espessura_minima_e_aplicada_na_modelagem()
        {
            TelhaTp40 telha = TelhaTp40.CriarPadrao();
            List<Ponto2DMm> fina = SecaoTelhaTp40.Contorno(telha, 0.43);
            List<Ponto2DMm> grossa = SecaoTelhaTp40.Contorno(telha, 2.0);

            Assert.Equal(
                fina.Max(p => p.YMm),
                grossa.Max(p => p.YMm) - 2.0 + SecaoTelhaTp40.EspessuraMinimaModeladaMm,
                6);
        }

        [Fact]
        public void Telha_invalida_lanca()
        {
            var telha = TelhaTp40.CriarPadrao();
            telha.AlturaOndaMm = new ValorCatalogo("AlturaOndaMm", 0.0, "mm", OrigemValor.Provisorio, "teste");

            Assert.Throws<System.ArgumentException>(() => SecaoTelhaTp40.Contorno(telha, 0.43));
        }

        [Fact]
        public void Laje_umida_tem_camadas_de_argamassa_telha_e_viga()
        {
            CamadasLaje camadas = CamadasLaje.CalcularUmida(3000.0, 200.0, 40.0, 30.0);

            Assert.NotNull(camadas.Camada(CamadaLaje.NomeArgamassa));
            Assert.NotNull(camadas.Camada(CamadaLaje.NomeTelha));
            Assert.NotNull(camadas.Camada(CamadaLaje.NomeViga));
        }
    }
}
