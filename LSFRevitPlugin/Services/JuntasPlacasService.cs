using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Le as juntas horizontais REAIS das placas ja geradas numa parede, em cm a partir da base
    /// dela, para o blocking poder alinhar as fileiras a essas juntas.
    ///
    /// Nao inventa valores: quando a parede nao tem placas (ou tem uma unica placa, sem junta),
    /// devolve lista vazia e quem chama cai para a distribuicao configurada, com aviso.
    ///
    /// A junta horizontal e o limite em Z entre duas placas empilhadas. A base do revestimento
    /// (limite inferior) e o topo (limite superior) sao descartados: nao sao juntas.
    /// </summary>
    public static class JuntasPlacasService
    {
        /// <summary>Tolerancia, em cm, para considerar dois limites como a mesma junta.</summary>
        public const double ToleranciaCm = 0.1;

        public static List<double> LerAlturasJuntasHorizontaisCm(
            Document doc, Wall parede, List<string> avisos = null)
        {
            var alturas = new List<double>();
            if (doc == null || parede == null) return alturas;

            LocationCurve curva = parede.Location as LocationCurve;
            Line linha = curva != null ? curva.Curve as Line : null;
            if (linha == null) return alturas;

            int paredeId = parede.Id.IntegerValue;
            double baseInterna = linha.GetEndPoint(0).Z;
            double baseMm = LeitorDeEstrutura.Mm(baseInterna);

            XYZ inicio = linha.GetEndPoint(0);
            XYZ direcao = (linha.GetEndPoint(1) - inicio).Normalize();
            XYZ normal = new XYZ(-direcao.Y, direcao.X, 0).Normalize();
            XYZ[] eixo = new[] { direcao, normal };

            PlacasExistentes existentes = PlacasExistentes.Ler(doc);

            var zRelativosCm = new List<double>();
            int placas = 0;

            foreach (PlacaExistente placa in existentes.Todas)
            {
                if (placa.Elemento == null || placa.ParedeId != paredeId) continue;

                MedidaElemento medida = LeitorDeEstrutura.Medir(placa.Elemento, eixo);
                if (medida == null) continue;

                placas++;
                zRelativosCm.Add((medida.Z0Mm - baseMm) / 10.0);
                zRelativosCm.Add((medida.Z1Mm - baseMm) / 10.0);
            }

            if (placas < 2 || zRelativosCm.Count == 0) return alturas;

            double min = zRelativosCm.Min();
            double max = zRelativosCm.Max();
            if (max - min <= ToleranciaCm) return alturas;

            foreach (double z in zRelativosCm)
            {
                if (z <= min + ToleranciaCm || z >= max - ToleranciaCm) continue;
                if (alturas.Any(a => Math.Abs(a - z) <= ToleranciaCm)) continue;
                alturas.Add(z);
            }

            alturas.Sort();

            if (avisos != null && alturas.Count > 0)
            {
                avisos.Add("Blocking: alinhamento às juntas das placas — "
                    + alturas.Count + " junta(s) horizontal(is) encontrada(s) nas placas.");
            }

            return alturas;
        }
    }
}
