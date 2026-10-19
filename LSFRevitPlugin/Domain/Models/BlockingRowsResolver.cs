using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Resolve as alturas relativas (cm, da base da parede) das fileiras de blocking a partir da
    /// configuracao. Regras, nesta ordem:
    /// 1. alturas explicitas do usuario (AlturasRelativasCm) quando presentes -- independentes das
    ///    juntas das placas;
    /// 2. senao, alinhamento as juntas horizontais das placas (AlturasJuntasPlacasCm) quando
    ///    solicitado E ha dados de placas no projeto;
    /// 3. senao, distribuicao automatica de Fileiras (comportamento pre-existente).
    /// Sem dados de placas o plugin NUNCA presume alturas de junta: avisa e cai para o padrao.
    /// Alturas duplicadas, fora dos limites uteis da parede ou invalidas sao ignoradas com aviso.
    /// </summary>
    public static class BlockingRowsResolver
    {
        /// <summary>Tolerancia, em cm, para considerar duas alturas como duplicadas.</summary>
        public const double ToleranciaDuplicadaCm = 0.1;

        /// <summary>
        /// Devolve as alturas validas, relativas a base da parede, em cm, ordenadas. Nunca
        /// retorna nulo. Registra em <paramref name="avisos"/> os motivos de descarte.
        /// </summary>
        public static List<double> ResolverAlturasRelativasCm(
            BlockingConfig config,
            double alturaParedeUtilCm,
            double folgaGuiasCm,
            List<string> avisos)
        {
            var alturas = new List<double>();
            if (config == null || !config.Habilitado) return alturas;
            if (avisos == null) avisos = new List<string>();

            List<double> usuario = config.AlturasRelativasCm ?? new List<double>();
            bool temJuntas = config.AlturasJuntasPlacasCm != null && config.AlturasJuntasPlacasCm.Count > 0;

            List<double> candidatas;
            if (usuario.Count > 0)
            {
                candidatas = new List<double>(usuario);
            }
            else if (config.AlinharJuntasPlacas && temJuntas)
            {
                candidatas = new List<double>(config.AlturasJuntasPlacasCm);
            }
            else
            {
                if (config.AlinharJuntasPlacas)
                {
                    avisos.Add("Blocking: alinhamento às juntas das placas marcado, mas não há juntas de placas neste projeto; usadas as fileiras configuradas/padrão (nenhuma altura presumida).");
                }

                if (config.Fileiras <= 0) return alturas;

                double zBase = folgaGuiasCm;
                double zTopo = alturaParedeUtilCm - folgaGuiasCm;
                if (zTopo <= zBase + 1e-6) return alturas;

                candidatas = new List<double>();
                for (int f = 1; f <= config.Fileiras; f++)
                {
                    double tZ = config.Fileiras == 1 ? 0.5 : (double)f / (double)(config.Fileiras + 1);
                    candidatas.Add(zBase + (zTopo - zBase) * tZ);
                }
            }

            double minOk = folgaGuiasCm + 1e-6;
            double maxOk = alturaParedeUtilCm - folgaGuiasCm - 1e-6;
            CultureInfo inv = CultureInfo.InvariantCulture;

            foreach (double h in candidatas)
            {
                if (double.IsNaN(h) || double.IsInfinity(h)) continue;

                if (h < minOk - 1e-6 || h > maxOk + 1e-6)
                {
                    avisos.Add("Blocking: altura "
                        + h.ToString("F1", inv)
                        + " cm fora dos limites úteis da parede ("
                        + Math.Max(minOk, 0).ToString("F1", inv) + " a "
                        + Math.Min(maxOk, alturaParedeUtilCm).ToString("F1", inv)
                        + " cm) e foi ignorada.");
                    continue;
                }

                if (alturas.Any(e => Math.Abs(e - h) <= ToleranciaDuplicadaCm))
                {
                    avisos.Add("Blocking: altura duplicada " + h.ToString("F1", inv) + " cm ignorada.");
                    continue;
                }

                alturas.Add(h);
            }

            alturas.Sort();
            return alturas;
        }
    }
}