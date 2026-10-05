using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Caixa de um elemento nos eixos da parede dele (U ao longo da parede, N atravessando,
    /// Z vertical), em mm, mais os 8 cantos dessa caixa no mundo (unidades internas do Revit).
    /// </summary>
    public sealed class MedidaElemento
    {
        public XYZ Dir { get; set; }
        public XYZ Nor { get; set; }
        public double U0Mm { get; set; }
        public double U1Mm { get; set; }
        public double N0Mm { get; set; }
        public double N1Mm { get; set; }
        public double Z0Mm { get; set; }
        public double Z1Mm { get; set; }
        public double CentroXMm { get; set; }
        public double CentroYMm { get; set; }
        public XYZ[] Cantos { get; set; }

        /// <summary>true quando a maior das tres dimensoes e a altura.</summary>
        public bool Vertical { get; set; }

        /// <summary>A maior das tres dimensoes (o comprimento de um perfil e sempre maior que a secao dele).</summary>
        public double ComprimentoMm { get; set; }
    }

    /// <summary>Um elemento de estrutura gerado (montante, guia, reforco de vao...), lido do projeto.</summary>
    public sealed class ElementoEstruturaLido
    {
        public int ElementoId { get; set; }
        public int ParedeId { get; set; }
        public int Painel { get; set; }
        public string Tipo { get; set; }
        public string Perfil { get; set; }
        public double ComprimentoMm { get; set; }
        public MedidaElemento Medida { get; set; }

        /// <summary>Montante em pe (nao e guia nem peca horizontal como verga e contra-verga).</summary>
        public bool EhMontanteVertical
        {
            get
            {
                return Medida != null && Medida.Vertical
                    && !Tipo.StartsWith("Guia", StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Leitura da estrutura gerada pelo plugin (elementos etiquetados no campo Comentarios).</summary>
    public static class LeitorDeEstrutura
    {
        public const string PrefixoEstrutura = "LSF | Parede ";

        public static List<ElementoEstruturaLido> Ler(Document doc, List<string> avisos)
        {
            var lista = new List<ElementoEstruturaLido>();
            var eixos = new Dictionary<int, XYZ[]>();

            foreach (DirectShape ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>())
            {
                string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                if (comentario == null || !comentario.StartsWith(PrefixoEstrutura, StringComparison.Ordinal)) continue;

                string[] seg = comentario.Split(new[] { " | " }, StringSplitOptions.None);
                int paredeId;
                if (seg.Length < 4
                    || !int.TryParse(seg[1].Replace("Parede ", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out paredeId))
                {
                    continue;
                }

                int painel;
                if (!int.TryParse(seg[2].Replace("Painel ", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out painel))
                {
                    painel = 0;
                }

                XYZ[] eixo;
                if (!eixos.TryGetValue(paredeId, out eixo))
                {
                    eixo = EixoDaParede(doc, paredeId);
                    eixos[paredeId] = eixo;
                }

                MedidaElemento medida = Medir(ds, eixo);
                if (medida == null || medida.ComprimentoMm <= 0)
                {
                    avisos.Add("Elemento " + ds.Id + ": sem geometria, ignorado.");
                    continue;
                }

                lista.Add(new ElementoEstruturaLido
                {
                    ElementoId = ds.Id.IntegerValue,
                    ParedeId = paredeId,
                    Painel = painel,
                    Tipo = seg[3],
                    Perfil = seg.Length > 4 ? string.Join(" | ", seg.Skip(4)) : "",
                    ComprimentoMm = medida.ComprimentoMm,
                    Medida = medida
                });
            }

            return lista;
        }

        /// <summary>{direcao, normal} da parede, ou nulo se ela nao for reta.</summary>
        public static XYZ[] EixoDaParede(Document doc, int paredeId)
        {
            Wall parede = doc.GetElement(new ElementId(paredeId)) as Wall;
            LocationCurve curva = parede != null ? parede.Location as LocationCurve : null;
            Line linha = curva != null ? curva.Curve as Line : null;
            if (linha == null) return null;

            XYZ direcao = (linha.GetEndPoint(1) - linha.GetEndPoint(0)).Normalize();
            XYZ normal = new XYZ(-direcao.Y, direcao.X, 0).Normalize();
            return new[] { direcao, normal };
        }

        /// <summary>
        /// Caixa do elemento nos eixos da parede, em mm. Sem eixo (parede curva), usa os eixos X e Y do projeto.
        /// </summary>
        public static MedidaElemento Medir(Element elemento, XYZ[] eixo)
        {
            GeometryElement geometria = elemento.get_Geometry(
                new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false });
            if (geometria == null) return null;

            var pontos = new List<XYZ>();
            ColetarPontos(geometria, pontos);
            if (pontos.Count == 0) return null;

            XYZ dir = eixo != null ? eixo[0] : XYZ.BasisX;
            XYZ nor = eixo != null ? eixo[1] : XYZ.BasisY;

            double u0 = pontos.Min(p => p.DotProduct(dir));
            double u1 = pontos.Max(p => p.DotProduct(dir));
            double n0 = pontos.Min(p => p.DotProduct(nor));
            double n1 = pontos.Max(p => p.DotProduct(nor));
            double z0 = pontos.Min(p => p.Z);
            double z1 = pontos.Max(p => p.Z);

            var medida = new MedidaElemento
            {
                Dir = dir,
                Nor = nor,
                U0Mm = Mm(u0),
                U1Mm = Mm(u1),
                N0Mm = Mm(n0),
                N1Mm = Mm(n1),
                Z0Mm = Mm(z0),
                Z1Mm = Mm(z1)
            };

            double dx = medida.U1Mm - medida.U0Mm;
            double dy = medida.N1Mm - medida.N0Mm;
            double dz = medida.Z1Mm - medida.Z0Mm;

            medida.ComprimentoMm = Math.Max(dz, Math.Max(dx, dy));
            medida.Vertical = dz >= dx && dz >= dy;

            XYZ centro = dir.Multiply((u0 + u1) / 2.0) + nor.Multiply((n0 + n1) / 2.0);
            medida.CentroXMm = Mm(centro.X);
            medida.CentroYMm = Mm(centro.Y);

            var cantos = new List<XYZ>();
            foreach (double u in new[] { u0, u1 })
            {
                foreach (double n in new[] { n0, n1 })
                {
                    foreach (double z in new[] { z0, z1 })
                    {
                        cantos.Add(dir.Multiply(u) + nor.Multiply(n) + XYZ.BasisZ.Multiply(z));
                    }
                }
            }

            medida.Cantos = cantos.ToArray();
            return medida;
        }

        /// <summary>Os solidos (com volume) de um elemento.</summary>
        public static List<Solid> ColetarSolidos(Element elemento)
        {
            var lista = new List<Solid>();
            GeometryElement geometria = elemento.get_Geometry(
                new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false });
            if (geometria != null) ColetarSolidos(geometria, lista);
            return lista;
        }

        private static void ColetarSolidos(GeometryElement geometria, List<Solid> lista)
        {
            foreach (GeometryObject objeto in geometria)
            {
                Solid solido = objeto as Solid;
                if (solido != null)
                {
                    if (solido.Volume > 0) lista.Add(solido);
                    continue;
                }

                GeometryInstance instancia = objeto as GeometryInstance;
                if (instancia != null) ColetarSolidos(instancia.GetInstanceGeometry(), lista);
            }
        }

        private static void ColetarPontos(GeometryElement geometria, List<XYZ> pontos)
        {
            foreach (GeometryObject objeto in geometria)
            {
                Solid solido = objeto as Solid;
                if (solido != null)
                {
                    foreach (Edge aresta in solido.Edges)
                    {
                        pontos.AddRange(aresta.Tessellate());
                    }

                    continue;
                }

                GeometryInstance instancia = objeto as GeometryInstance;
                if (instancia != null) ColetarPontos(instancia.GetInstanceGeometry(), pontos);
            }
        }

        /// <summary>Unidade interna do Revit para milimetros.</summary>
        public static double Mm(double valorInterno)
        {
            return RevitVersionAdapter.InternoParaCentimetros(valorInterno) * 10.0;
        }
    }
}