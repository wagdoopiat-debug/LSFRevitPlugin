using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Boards;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// O que as placas precisam saber de uma parede ja gerada: os paineis, a altura, a posicao
    /// real dos montantes de apoio, as aberturas e a espessura da estrutura. As posicoes sao
    /// medidas ao longo do eixo a partir da ponta inicial, em mm.
    /// </summary>
    public sealed class LeituraParede
    {
        public Wall Parede { get; set; }

        /// <summary>Ponta inicial da parede (na cota da linha de localizacao).</summary>
        public XYZ Inicio { get; set; }

        /// <summary>Direcao do eixo da parede (da ponta inicial para a final).</summary>
        public XYZ Direcao { get; set; }

        /// <summary>Normal horizontal da parede (lado da face 1).</summary>
        public XYZ Normal { get; set; }

        public double ComprimentoMm { get; set; }

        public double AlturaMm { get; set; }

        /// <summary>true se a parede tem elementos de estrutura gerados.</summary>
        public bool TemEstrutura { get; set; }

        /// <summary>Limite da estrutura no lado oposto a Normal, em mm, a partir do eixo (negativo).</summary>
        public double EstruturaNMinMm { get; set; }

        /// <summary>Limite da estrutura no lado da Normal, em mm, a partir do eixo (positivo).</summary>
        public double EstruturaNMaxMm { get; set; }

        /// <summary>Onde a estrutura comeca ao longo da parede, em mm (negativo se passa da ponta inicial).</summary>
        public double EstruturaUMinMm { get; set; }

        /// <summary>Onde a estrutura termina ao longo da parede, em mm (maior que o comprimento se passa da ponta final).</summary>
        public double EstruturaUMaxMm { get; set; }

        /// <summary>true se o comprimento da parede mudou depois de painelizar (a painelizacao gravada nao vale).</summary>
        public bool PainelizacaoDesatualizada { get; set; }

        /// <summary>Paineis da parede, em unidades internas do Revit (um unico painel se nao ha painelizacao).</summary>
        public List<PainelInfo> Paineis { get; set; } = new List<PainelInfo>();

        /// <summary>Posicao dos montantes de apoio, em mm, ao longo da parede, ordenadas.</summary>
        public List<double> MontantesMm { get; set; } = new List<double>();

        /// <summary>Aberturas (portas e janelas) com a medida automatica da esquadria, em mm.</summary>
        public List<RetanguloMm> AberturasMm { get; set; } = new List<RetanguloMm>();
    }

    /// <summary>
    /// Le a estrutura que a geracao criou numa parede. So le: nao cria nem apaga nada.
    /// </summary>
    public static class LeituraEstruturaParedeService
    {
        // Montante de apoio de junta = elemento vertical: altura pelo menos o dobro do maior
        // tamanho na horizontal. Entram montantes, king, jack e cripples; ficam de fora as
        // guias, a verga e a contra-verga (compridas e baixas).
        private const double RazaoMinimaAlturaPorLargura = 2.0;

        public static LeituraParede Ler(Document doc, Wall parede)
        {
            LocationCurve curva = parede.Location as LocationCurve;
            Line linha = curva != null ? curva.Curve as Line : null;
            if (linha == null)
            {
                throw new InvalidOperationException("Paredes curvas ainda nao sao suportadas nas placas.");
            }

            Parameter parametroAltura = parede.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (parametroAltura == null)
            {
                throw new InvalidOperationException("Nao foi possivel ler a altura da parede.");
            }

            XYZ inicio = linha.GetEndPoint(0);
            XYZ direcao = (linha.GetEndPoint(1) - inicio).Normalize();
            XYZ normalBruta = parede.Orientation;
            XYZ normal = new XYZ(normalBruta.X, normalBruta.Y, 0).Normalize();

            bool desatualizada;
            List<PainelInfo> paineis = PainelizacaoService.ObterPaineis(parede, out desatualizada);

            // Mesmo texto que a geracao grava no campo Comentarios de cada elemento da parede.
            string prefixo = "LSF | Parede " + parede.Id + " | ";

            var montantes = new List<double>();
            bool temEstrutura = false;
            double nMin = double.MaxValue;
            double nMax = double.MinValue;
            double uMin = double.MaxValue;
            double uMax = double.MinValue;

            foreach (DirectShape ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>())
            {
                string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                if (comentario == null || !comentario.StartsWith(prefixo, StringComparison.Ordinal)) continue;

                var extremos = new Extremos();
                GeometryElement geometria = ds.get_Geometry(
                    new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false });
                if (geometria == null) continue;

                Coletar(geometria, extremos, inicio, direcao, normal);
                if (!extremos.Tem) continue;

                temEstrutura = true;
                nMin = Math.Min(nMin, extremos.NMin);
                nMax = Math.Max(nMax, extremos.NMax);
                uMin = Math.Min(uMin, extremos.UMin);
                uMax = Math.Max(uMax, extremos.UMax);

                double altura = extremos.ZMax - extremos.ZMin;
                double larguraHorizontal = Math.Max(
                    extremos.UMax - extremos.UMin,
                    extremos.NMax - extremos.NMin);

                if (altura < larguraHorizontal * RazaoMinimaAlturaPorLargura) continue;

                montantes.Add(ParaMm((extremos.UMin + extremos.UMax) / 2.0));
            }

            montantes.Sort();

            double alturaInterna = parametroAltura.AsDouble();

            var aberturas = new List<RetanguloMm>();
            var reforco = new OpeningReinforcement();
            foreach (var vao in reforco.DetectarVaos(doc, parede))
            {
                double centro = (vao.PontoCentral - inicio).DotProduct(direcao);
                double u0 = centro - vao.LarguraInterna / 2.0;
                double u1 = centro + vao.LarguraInterna / 2.0;
                double z0 = vao.AlturaSoleiraInterna;
                double z1 = vao.AlturaSoleiraInterna + vao.AlturaInterna;

                aberturas.Add(new RetanguloMm(ParaMm(u0), ParaMm(u1), ParaMm(z0), ParaMm(z1)));
            }

            return new LeituraParede
            {
                Parede = parede,
                Inicio = inicio,
                Direcao = direcao,
                Normal = normal,
                ComprimentoMm = ParaMm(linha.Length),
                AlturaMm = ParaMm(alturaInterna),
                TemEstrutura = temEstrutura,
                EstruturaNMinMm = temEstrutura ? ParaMm(nMin) : 0.0,
                EstruturaNMaxMm = temEstrutura ? ParaMm(nMax) : 0.0,
                EstruturaUMinMm = temEstrutura ? ParaMm(uMin) : 0.0,
                EstruturaUMaxMm = temEstrutura ? ParaMm(uMax) : ParaMm(linha.Length),
                PainelizacaoDesatualizada = desatualizada,
                Paineis = paineis,
                MontantesMm = montantes,
                AberturasMm = aberturas
            };
        }

        private static double ParaMm(double valorInterno)
        {
            return RevitVersionAdapter.InternoParaCentimetros(valorInterno) * 10.0;
        }

        /// <summary>Limites de um elemento no sistema da parede (U ao longo, N perpendicular, Z vertical), em unidades internas.</summary>
        private sealed class Extremos
        {
            public double UMin = double.MaxValue;
            public double UMax = double.MinValue;
            public double NMin = double.MaxValue;
            public double NMax = double.MinValue;
            public double ZMin = double.MaxValue;
            public double ZMax = double.MinValue;
            public bool Tem;

            public void Adicionar(XYZ ponto, XYZ inicio, XYZ direcao, XYZ normal)
            {
                XYZ relativo = ponto - inicio;
                double u = relativo.DotProduct(direcao);
                double n = relativo.DotProduct(normal);
                double z = relativo.Z;

                UMin = Math.Min(UMin, u);
                UMax = Math.Max(UMax, u);
                NMin = Math.Min(NMin, n);
                NMax = Math.Max(NMax, n);
                ZMin = Math.Min(ZMin, z);
                ZMax = Math.Max(ZMax, z);
                Tem = true;
            }
        }

        private static void Coletar(
            GeometryElement geometria, Extremos extremos, XYZ inicio, XYZ direcao, XYZ normal)
        {
            foreach (GeometryObject objeto in geometria)
            {
                Solid solido = objeto as Solid;
                if (solido != null)
                {
                    if (solido.Edges.Size == 0) continue;

                    foreach (Edge aresta in solido.Edges)
                    {
                        foreach (XYZ ponto in aresta.Tessellate())
                        {
                            extremos.Adicionar(ponto, inicio, direcao, normal);
                        }
                    }

                    continue;
                }

                GeometryInstance instancia = objeto as GeometryInstance;
                if (instancia != null)
                {
                    Coletar(instancia.GetInstanceGeometry(), extremos, inicio, direcao, normal);
                }
            }
        }
    }
}