using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Detecta vaos (portas e janelas) ja existentes numa parede e calcula o reforco ao redor
    /// deles (King Studs, Jack Studs, verga na soleira, contra-verga no topo e cripple studs
    /// nas faixas vazias acima da contra-verga e abaixo da verga).
    /// </summary>
    public class OpeningReinforcement
    {
        private const string PrefixoVao = "LSF-Vao";

        /// <summary>
        /// Folga de encaixe entre uma peca vertical e a guia (incluindo verga/contra-verga,
        /// que sao guias horizontais de verdade, construidas via GuiaBuilder) em que ela se
        /// encaixa, em cm -- o encaixe fisico nunca e perfeito.
        /// </summary>
        public const double FolgaEncaixeGuiaCm = 0.5;

        public List<OpeningInfo> DetectarVaos(Document doc, Wall parede)
        {
            var resultado = new List<OpeningInfo>();

            var instancias = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Host != null && fi.Host.Id == parede.Id)
                .Where(fi => fi.Category != null &&
                    (fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors
                     || fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows));

            foreach (FamilyInstance instancia in instancias)
            {
                LocationPoint locationPoint = instancia.Location as LocationPoint;
                if (locationPoint == null)
                    continue;

                XYZ pontoInstancia = locationPoint.Point;

                LocationCurve curvaParede = parede.Location as LocationCurve;
                if (curvaParede == null)
                    continue;

                XYZ pontoNoEixo = curvaParede.Curve.Project(pontoInstancia).XYZPoint;

                double largura = instancia.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble() ?? 0;
                double altura = instancia.Symbol.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble() ?? 0;

                bool ehPorta = instancia.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors;
                double alturaSoleira = ehPorta
                    ? 0
                    : instancia.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.AsDouble() ?? 0;

                resultado.Add(new OpeningInfo
                {
                    ElementoId = instancia.Id,
                    Categoria = ehPorta ? "Porta" : "Janela",
                    PontoCentral = pontoNoEixo,
                    LarguraInterna = largura,
                    AlturaInterna = altura,
                    AlturaSoleiraInterna = alturaSoleira
                });
            }

            return resultado;
        }

        public static string GerarNomeVao(ElementId idVao)
        {
            return $"{PrefixoVao}-{idVao.IntegerValue}";
        }

        /// <summary>
        /// Gera um nome unico para um cripple stud, derivado do vao a que pertence, da posicao
        /// (X/Y em mm, arredondados) no eixo da parede e de estar acima da contra-verga ou
        /// abaixo da verga/soleira. Comeca com o mesmo prefixo de GerarNomeVao, entao o cripple
        /// entra automaticamente na checagem de dedup "ja reforcado" usada para o vao inteiro.
        /// </summary>
        public static string GerarNomeCripple(ElementId idVao, XYZ pontoEixo, bool acima)
        {
            string sufixo = acima ? "acima" : "abaixo";
            int xMm = (int)Math.Round(RevitVersionAdapter.InternoParaCentimetros(pontoEixo.X) * 10);
            int yMm = (int)Math.Round(RevitVersionAdapter.InternoParaCentimetros(pontoEixo.Y) * 10);
            string coordX = xMm < 0 ? $"m{-xMm}" : xMm.ToString();
            string coordY = yMm < 0 ? $"m{-yMm}" : yMm.ToString();
            return $"{GerarNomeVao(idVao)}-cripple-{sufixo}-{coordX}_{coordY}";
        }

        private static XYZ SubirZ(XYZ ponto, double deltaZ)
        {
            return new XYZ(ponto.X, ponto.Y, ponto.Z + deltaZ);
        }

        /// <summary>
        /// Calcula os elementos de reforco de um vao: 2 King Studs, 2 Jack Studs, 1 contra-verga
        /// (guia horizontal no topo do vao) e, se for janela, 1 verga (guia horizontal na
        /// soleira). Usa a alma e a aba do perfil escolhido (PerfilSpec) tanto para as dimensoes
        /// dos montantes verticais quanto para a secao das guias horizontais -- verga e
        /// contra-verga sao construidas via GuiaBuilder, com alma perpendicular a parede e
        /// abas na vertical, e se estendem da borda externa de um Jack Stud a borda externa do
        /// outro (cobrindo os dois Jack Studs por completo -- e neles que a verga/contra-verga
        /// de fato se apoia).
        ///
        /// Orientacao do "C" dos King/Jack: a boca do "C" aponta para a Direcao do elemento.
        /// Nos dois lados do vao a boca aponta PARA FORA do vao, de modo que a ALMA fica voltada
        /// para o vao (e nela que se parafusa a placa/batente -- na boca nao da).
        ///
        /// Orientacao do "U": a contra-verga abre para CIMA (alma embaixo, no topo do vao) e os
        /// cripples de cima entram por dentro dela; a verga abre para BAIXO (alma em cima, na
        /// soleira) e os cripples de baixo entram por dentro dela.
        ///
        /// Encaixe (folga de ~0,5cm por contato):
        /// - King Stud: toca as duas guias principais da parede (base e topo), embutindo por
        ///   toda a extensao de cada uma, igual aos montantes normais.
        /// - Jack Stud: toca a guia inferior da parede embaixo (embute total). No topo, NAO
        ///   entra na contra-verga -- ela apenas se apoia sobre o Jack Stud (like um parafusamento
        ///   por baixo), entao o Jack para logo antes da face inferior da contra-verga, com a
        ///   folga de encaixe. Quem de fato entra na contra-verga e o cripple stud de cima (ver
        ///   CalcularCrippleStuds) -- assim os dois se referenciam na mesma face (a base da
        ///   contra-verga) sem colidir entre si.
        /// - Verga e contra-verga em si nao recebem folga, sao as proprias guias.
        /// </summary>
        /// <param name="materialId">Material (cor) aplicado ao solido da verga e da contra-verga,
        /// que sao construidos aqui dentro (SolidoPreConstruido). Nulo = sem material. Os
        /// King/Jack nao usam este parametro: seus solidos sao construidos pelo chamador, que
        /// aplica o material la.</param>
        public List<ElementoReforcoVao> CalcularReforcoVao(
            OpeningInfo vao, XYZ direcaoPrincipal, double alturaParedeInterna, PerfilSpec perfil,
            ElementId materialId = null)
        {
            var resultado = new List<ElementoReforcoVao>();
            string nomeBase = GerarNomeVao(vao.ElementoId);

            double larguraPerfilCm = perfil.AbaMm / 10.0;
            double profundidadePerfilCm = perfil.LarguraAlmaMm / 10.0;

            double halfWInterna = RevitVersionAdapter.CentimetrosParaInterno(larguraPerfilCm / 2.0);
            double larguraPerfilInterna = RevitVersionAdapter.CentimetrosParaInterno(larguraPerfilCm);
            double folgaInterna = RevitVersionAdapter.CentimetrosParaInterno(FolgaEncaixeGuiaCm);
            double alturaGuiaAbaInterna = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);

            XYZ edgeStart = vao.PontoCentral - direcaoPrincipal.Multiply(vao.LarguraInterna / 2.0);
            XYZ edgeEnd = vao.PontoCentral + direcaoPrincipal.Multiply(vao.LarguraInterna / 2.0);

            XYZ jack1 = edgeStart - direcaoPrincipal.Multiply(halfWInterna);
            XYZ king1 = jack1 - direcaoPrincipal.Multiply(larguraPerfilInterna);
            XYZ jack2 = edgeEnd + direcaoPrincipal.Multiply(halfWInterna);
            XYZ king2 = jack2 + direcaoPrincipal.Multiply(larguraPerfilInterna);

            // Direcao = para onde a boca do "C" aponta. Lado do inicio do vao: o vao fica em
            // +direcaoPrincipal, entao a boca aponta para -direcaoPrincipal (alma para o vao).
            // Lado do fim do vao: o vao fica em -direcaoPrincipal, entao a boca aponta para
            // +direcaoPrincipal (alma para o vao).
            XYZ direcaoLadoInicio = direcaoPrincipal.Negate();
            XYZ direcaoLadoFim = direcaoPrincipal;

            double topoVaoInterna = vao.AlturaSoleiraInterna + vao.AlturaInterna;

            // King Studs: tocam guia inferior (base) e guia superior (topo) da parede.
            double alturaKingComFolga = alturaParedeInterna - (2 * folgaInterna);
            resultado.Add(new ElementoReforcoVao { Ponto = SubirZ(king1, folgaInterna), Direcao = direcaoLadoInicio, AlturaInterna = alturaKingComFolga, LarguraMm = larguraPerfilCm * 10, ProfundidadeMm = profundidadePerfilCm * 10, Nome = nomeBase + "-king1" });
            resultado.Add(new ElementoReforcoVao { Ponto = SubirZ(king2, folgaInterna), Direcao = direcaoLadoFim, AlturaInterna = alturaKingComFolga, LarguraMm = larguraPerfilCm * 10, ProfundidadeMm = profundidadePerfilCm * 10, Nome = nomeBase + "-king2" });

            // Jack Studs: tocam guia inferior embaixo (embute total). No topo, apenas se
            // aproximam da face inferior da contra-verga, sem entrar nela.
            double alturaJackComFolga = topoVaoInterna - (2 * folgaInterna);
            resultado.Add(new ElementoReforcoVao { Ponto = SubirZ(jack1, folgaInterna), Direcao = direcaoLadoInicio, AlturaInterna = alturaJackComFolga, LarguraMm = larguraPerfilCm * 10, ProfundidadeMm = profundidadePerfilCm * 10, Nome = nomeBase + "-jack1" });
            resultado.Add(new ElementoReforcoVao { Ponto = SubirZ(jack2, folgaInterna), Direcao = direcaoLadoFim, AlturaInterna = alturaJackComFolga, LarguraMm = larguraPerfilCm * 10, ProfundidadeMm = profundidadePerfilCm * 10, Nome = nomeBase + "-jack2" });

            // Verga/contra-verga se estendem da borda externa de jack1 (o lado que encosta no
            // King Stud) ate a borda externa de jack2 -- cobrem os dois Jack Studs por inteiro,
            // que e onde elas de fato se apoiam.
            XYZ inicioGuiaHorizontal = jack1 - direcaoPrincipal.Multiply(halfWInterna);
            double comprimentoGuiaHorizontalInterno = jack2.DistanceTo(jack1) + larguraPerfilInterna;

            // Contra-verga: guia horizontal no topo do vao, aberta para CIMA (alma embaixo).
            Solid solidoContraVerga = GuiaBuilder.CriarSolidoGuia(
                SubirZ(inicioGuiaHorizontal, topoVaoInterna), direcaoPrincipal, comprimentoGuiaHorizontalInterno, perfil, zBase: 0,
                materialId: materialId, abertoParaCima: true);
            resultado.Add(new ElementoReforcoVao { Nome = nomeBase + "-contraverga", SolidoPreConstruido = solidoContraVerga });

            if (vao.AlturaSoleiraInterna > 0)
            {
                // Verga: guia horizontal na soleira, aberta para BAIXO (alma em cima).
                double baseVergaInterna = vao.AlturaSoleiraInterna - alturaGuiaAbaInterna;
                Solid solidoVerga = GuiaBuilder.CriarSolidoGuia(
                    SubirZ(inicioGuiaHorizontal, baseVergaInterna), direcaoPrincipal, comprimentoGuiaHorizontalInterno, perfil, zBase: 0,
                    materialId: materialId, abertoParaCima: false);
                resultado.Add(new ElementoReforcoVao { Nome = nomeBase + "-verga", SolidoPreConstruido = solidoVerga });
            }

            return resultado;
        }

        /// <summary>
        /// Calcula os cripple studs de um vao: para cada posicao X da grade normal de montantes
        /// que caia dentro do vao, gera um montante curto acima da contra-verga (entre ela e a
        /// guia superior da parede) e, se for janela, outro abaixo da verga (entre a guia
        /// inferior da parede e a verga). Em porta, so a faixa de cima existe.
        ///
        /// Encaixe: o cripple de cima atravessa a contra-verga inteira, entrando por dentro
        /// dela e tocando sua alma (a face de baixo da contra-verga) com a folga de encaixe --
        /// e por cima embute por toda a extensao da guia superior da parede. O Jack Stud (ver
        /// CalcularReforcoVao) se referencia na mesma face de baixo da contra-verga, mas por
        /// fora dela, sem entrar -- assim os dois nao colidem. O cripple de baixo embute por
        /// toda a extensao da guia inferior por baixo e atravessa a verga inteira por cima,
        /// tocando a alma dela (unico contato da verga, sem colisao a evitar).
        /// </summary>
        public List<ElementoReforcoVao> CalcularCrippleStuds(
            OpeningInfo vao, List<XYZ> pontosDaGradeDentroDoVao, XYZ direcaoPrincipal,
            double alturaParedeInterna, PerfilSpec perfil)
        {
            var resultado = new List<ElementoReforcoVao>();
            if (pontosDaGradeDentroDoVao == null || pontosDaGradeDentroDoVao.Count == 0)
                return resultado;

            double larguraPerfilCm = perfil.AbaMm / 10.0;
            double profundidadePerfilCm = perfil.LarguraAlmaMm / 10.0;
            double folgaInterna = RevitVersionAdapter.CentimetrosParaInterno(FolgaEncaixeGuiaCm);
            double topoVaoInterna = vao.AlturaSoleiraInterna + vao.AlturaInterna;

            // Faixa acima: entra pela contra-verga inteira por baixo (toca a alma dela, a
            // face de baixo, com folga) e vai ate a guia superior da parede por cima.
            double baseCrippleAcimaInterna = topoVaoInterna + folgaInterna;
            double topoCrippleAcimaInterna = alturaParedeInterna - folgaInterna;
            double alturaCrippleAcima = topoCrippleAcimaInterna - baseCrippleAcimaInterna;

            // Faixa abaixo (so janela): da guia inferior da parede ate a verga, atravessando
            // ela inteira por cima (toca a alma dela, a face de cima, com folga).
            bool ehJanela = vao.AlturaSoleiraInterna > 0;
            double baseCrippleAbaixoInterna = folgaInterna;
            double topoCrippleAbaixoInterna = vao.AlturaSoleiraInterna - folgaInterna;
            double alturaCrippleAbaixo = ehJanela ? (topoCrippleAbaixoInterna - baseCrippleAbaixoInterna) : 0;

            const double tolerancia = 0.001;

            foreach (XYZ pontoEixo in pontosDaGradeDentroDoVao)
            {
                if (alturaCrippleAcima > tolerancia)
                {
                    resultado.Add(new ElementoReforcoVao
                    {
                        Ponto = SubirZ(pontoEixo, baseCrippleAcimaInterna),
                        Direcao = direcaoPrincipal,
                        AlturaInterna = alturaCrippleAcima,
                        LarguraMm = larguraPerfilCm * 10,
                        ProfundidadeMm = profundidadePerfilCm * 10,
                        Nome = GerarNomeCripple(vao.ElementoId, pontoEixo, acima: true)
                    });
                }

                if (ehJanela && alturaCrippleAbaixo > tolerancia)
                {
                    resultado.Add(new ElementoReforcoVao
                    {
                        Ponto = SubirZ(pontoEixo, baseCrippleAbaixoInterna),
                        Direcao = direcaoPrincipal,
                        AlturaInterna = alturaCrippleAbaixo,
                        LarguraMm = larguraPerfilCm * 10,
                        ProfundidadeMm = profundidadePerfilCm * 10,
                        Nome = GerarNomeCripple(vao.ElementoId, pontoEixo, acima: false)
                    });
                }
            }

            return resultado;
        }
    }
}