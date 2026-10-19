using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class OpeningReinforcementCrippleStudsTests
    {
        private readonly OpeningReinforcement _reforco = new OpeningReinforcement();
        private readonly XYZ _direcaoX = new XYZ(1, 0, 0);

        // Perfil Steel Frame 90mm -- aba (4cm) e alma (9cm) equivalentes aos valores fixos
        // que o codigo usava antes do PerfilSpec existir.
        private static readonly PerfilSpec PerfilPadrao = new PerfilSpec
        {
            Sistema = SistemaPerfil.SteelFrame,
            LarguraAlmaMm = 90,
            EspessuraChapaMm = 0.95
        };

        private const double FolgaCm = OpeningReinforcement.FolgaEncaixeGuiaCm;

        // Parede de 280cm de altura interna (convertida para pes, unidade interna do Revit).
        private static readonly double AlturaParedeInterna =
            RevitVersionAdapter.CentimetrosParaInterno(280);

        private OpeningInfo CriarPorta(double larguraCm = 90, double alturaCm = 210)
        {
            return new OpeningInfo
            {
                ElementoId = new ElementId(1001),
                Categoria = "Porta",
                PontoCentral = new XYZ(
                    RevitVersionAdapter.CentimetrosParaInterno(150), 0, 0),
                LarguraInterna = RevitVersionAdapter.CentimetrosParaInterno(larguraCm),
                AlturaInterna = RevitVersionAdapter.CentimetrosParaInterno(alturaCm),
                AlturaSoleiraInterna = 0
            };
        }

        private OpeningInfo CriarJanela(
            double larguraCm = 120, double alturaCm = 100, double soleiraCm = 90)
        {
            return new OpeningInfo
            {
                ElementoId = new ElementId(2002),
                Categoria = "Janela",
                PontoCentral = new XYZ(
                    RevitVersionAdapter.CentimetrosParaInterno(150), 0, 0),
                LarguraInterna = RevitVersionAdapter.CentimetrosParaInterno(larguraCm),
                AlturaInterna = RevitVersionAdapter.CentimetrosParaInterno(alturaCm),
                AlturaSoleiraInterna = RevitVersionAdapter.CentimetrosParaInterno(soleiraCm)
            };
        }

        private List<XYZ> PontosDaGrade(params double[] xCm)
        {
            return xCm
                .Select(x => new XYZ(RevitVersionAdapter.CentimetrosParaInterno(x), 0, 0))
                .ToList();
        }

        [Fact]
        public void ListaDePontosVazia_NaoGeraNenhumCripple()
        {
            var vao = CriarPorta();

            var resultado = _reforco.CalcularCrippleStuds(
                vao, new List<XYZ>(), _direcaoX, AlturaParedeInterna, PerfilPadrao);

            Assert.Empty(resultado);
        }

        [Fact]
        public void Porta_SoGeraCripplesAcima_NuncaAbaixo()
        {
            var vao = CriarPorta();
            var pontos = PontosDaGrade(120, 160);

            var resultado = _reforco.CalcularCrippleStuds(
                vao, pontos, _direcaoX, AlturaParedeInterna, PerfilPadrao);

            // 2 pontos, so faixa "acima" em porta (sem verga/soleira) -> 2 elementos
            Assert.Equal(2, resultado.Count);
            Assert.All(resultado, e => Assert.Contains("-acima-", e.Nome));
        }

        [Fact]
        public void Janela_GeraCripplesAcimaEAbaixo()
        {
            var vao = CriarJanela();
            var pontos = PontosDaGrade(120, 160);

            var resultado = _reforco.CalcularCrippleStuds(
                vao, pontos, _direcaoX, AlturaParedeInterna, PerfilPadrao);

            // 2 pontos x 2 faixas (acima da contra-verga + abaixo da verga) = 4 elementos
            Assert.Equal(4, resultado.Count);
            Assert.Equal(2, resultado.Count(e => e.Nome.Contains("-acima-")));
            Assert.Equal(2, resultado.Count(e => e.Nome.Contains("-abaixo-")));
        }

        [Fact]
        public void Janela_AlturaCrippleAbaixo_AtravessaAVergaInteiraComFolga()
        {
            // O cripple de baixo atravessa a verga inteira por cima (toca a alma dela, a
            // face de cima, com folga) -- altura vai da guia inferior da parede (com folga)
            // ate a soleira (com folga) -- soleira - 2*folga.
            var vao = CriarJanela(soleiraCm: 90);
            var pontos = PontosDaGrade(120);

            var resultado = _reforco.CalcularCrippleStuds(
                vao, pontos, _direcaoX, AlturaParedeInterna, PerfilPadrao);

            var crippleAbaixo = resultado.Single(e => e.Nome.Contains("-abaixo-"));
            double alturaEsperadaCm = 90 - (2 * FolgaCm);
            double alturaObtidaCm = RevitVersionAdapter.InternoParaCentimetros(crippleAbaixo.AlturaInterna);

            Assert.Equal(alturaEsperadaCm, alturaObtidaCm, precision: 3);
        }

        [Fact]
        public void Cripple_AlturaAcima_AtravessaAContraVergaInteiraComFolga()
        {
            // O cripple de cima atravessa a contra-verga inteira por baixo (toca a alma
            // dela, a face de baixo, com folga) e vai ate a guia superior da parede por cima
            // (tambem com folga) -- altura = alturaParede - topoDoVao - 2*folga.
            var vao = CriarPorta(alturaCm: 210); // soleira = 0, topo do vao = 210cm
            var pontos = PontosDaGrade(120);

            var resultado = _reforco.CalcularCrippleStuds(
                vao, pontos, _direcaoX, AlturaParedeInterna, PerfilPadrao);

            var crippleAcima = resultado.Single();
            double alturaEsperadaCm = 280 - 210 - (2 * FolgaCm);
            double alturaObtidaCm = RevitVersionAdapter.InternoParaCentimetros(crippleAcima.AlturaInterna);

            Assert.Equal(alturaEsperadaCm, alturaObtidaCm, precision: 3);
        }

        [Fact]
        public void VaoQuaseNoTopoDaParede_NaoGeraCrippleAcima_PorAlturaInsuficiente()
        {
            // Porta com topo do vao igual a altura da parede: nao sobra faixa acima
            // (280 - 280 - 1 < 0) -- nao deve gerar cripple ali.
            var vao = CriarPorta(alturaCm: 280);
            var pontos = PontosDaGrade(120);

            var resultado = _reforco.CalcularCrippleStuds(
                vao, pontos, _direcaoX, AlturaParedeInterna, PerfilPadrao);

            Assert.Empty(resultado);
        }

        [Fact]
        public void NomesDosCripples_SaoUnicosPorPosicaoEFaixa()
        {
            var vao = CriarJanela();
            var pontos = PontosDaGrade(120, 160, 200);

            var resultado = _reforco.CalcularCrippleStuds(
                vao, pontos, _direcaoX, AlturaParedeInterna, PerfilPadrao);

            var nomes = resultado.Select(e => e.Nome).ToList();
            Assert.Equal(nomes.Count, nomes.Distinct().Count());
        }

        [Fact]
        public void GerarNomeCripple_ComecaComMesmoPrefixoDoVao()
        {
            var idVao = new ElementId(3003);
            var ponto = new XYZ(RevitVersionAdapter.CentimetrosParaInterno(120), 0, 0);

            string nomeCripple = OpeningReinforcement.GerarNomeCripple(idVao, ponto, acima: true);
            string nomeVao = OpeningReinforcement.GerarNomeVao(idVao);

            Assert.StartsWith(nomeVao, nomeCripple);
        }
    }
}