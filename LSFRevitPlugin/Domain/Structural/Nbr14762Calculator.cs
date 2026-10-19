using System;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Domain.Structural
{
    /// <summary>
    /// Motor de calculo estrutural para perfis formados a frio conforme metodo da largura efetiva,
    /// base do dimensionamento adotado pela ABNT NBR 14762.
    ///
    /// AVISO IMPORTANTE: os coeficientes e formulas simplificadas aqui (especialmente na verificacao
    /// distorcional) precisam ser CONFERIDOS por engenheiro estrutural contra o texto oficial da norma
    /// antes de qualquer uso em projeto real. Este codigo e um ponto de partida, nao um substituto
    /// para responsabilidade tecnica.
    /// </summary>
    public class Nbr14762Calculator
    {
        private const double CoeficientePoisson = 0.3;

        /// <summary>
        /// Verifica um perfil submetido a compressao/flexo-compressao contra os 3 modos de flambagem
        /// (local, distorcional, global) e contra o limite de flecha de servico.
        /// </summary>
        public ResultadoVerificacao VerificarPerfil(
            PerfilLSF perfil,
            double comprimentoDestravadoMm,
            double cargaAxialN,
            double momentoFletorNmm,
            double flechaCalculadaMm)
        {
            var local = VerificarFlambagemLocal(perfil, cargaAxialN);
            if (!local.Aprovado) return local;

            var distorcional = VerificarFlambagemDistorcional(perfil, cargaAxialN);
            if (!distorcional.Aprovado) return distorcional;

            var global = VerificarFlambagemGlobal(perfil, comprimentoDestravadoMm, cargaAxialN, momentoFletorNmm);
            if (!global.Aprovado) return global;

            var flecha = VerificarFlecha(comprimentoDestravadoMm, flechaCalculadaMm);
            if (!flecha.Aprovado) return flecha;

            return new ResultadoVerificacao
            {
                Aprovado = true,
                ModoCritico = "Nenhum",
                TaxaUtilizacao = Math.Max(local.TaxaUtilizacao,
                                  Math.Max(distorcional.TaxaUtilizacao,
                                  Math.Max(global.TaxaUtilizacao, flecha.TaxaUtilizacao))),
                Observacao = "Perfil aprovado em todos os modos de verificacao."
            };
        }

        /// <summary>
        /// Flambagem local pelo metodo da largura efetiva (Winter). Considera a mesa como o elemento
        /// mais critico (simplificacao inicial -- o caso completo tambem verifica alma e enrijecedor
        /// separadamente e soma as reducoes).
        /// </summary>
        public ResultadoVerificacao VerificarFlambagemLocal(PerfilLSF perfil, double cargaAxialN)
        {
            double b = perfil.LarguraMesaMm;
            double t = perfil.EspessuraMm;
            double fy = perfil.TensaoEscoamentoMPa;
            double E = perfil.ModuloElasticidadeMPa;

            // Coeficiente de flambagem local para placa com um bordo livre e outro apoiado
            // (mesa com enrijecedor de borda) -- valor simplificado, CONFIRME contra a tabela da norma
            // conforme a condicao real de apoio de cada elemento do perfil.
            const double kSigma = 4.0;

            double fcr = kSigma * Math.PI * Math.PI * E /
                         (12.0 * (1.0 - CoeficientePoisson * CoeficientePoisson)) *
                         Math.Pow(t / b, 2);

            double esbeltezReduzida = Math.Sqrt(fy / fcr);

            double fatorReducao;
            if (esbeltezReduzida <= 0.673)
            {
                fatorReducao = 1.0; // secao nao sofre reducao por flambagem local
            }
            else
            {
                fatorReducao = (1.0 - 0.22 / esbeltezReduzida) / esbeltezReduzida;
            }

            double areaEfetivaMm2 = perfil.AreaMm2 * fatorReducao;
            double cargaResistenteN = areaEfetivaMm2 * fy;

            double taxa = cargaAxialN / cargaResistenteN;

            return new ResultadoVerificacao
            {
                Aprovado = taxa <= 1.0,
                ModoCritico = taxa <= 1.0 ? "Nenhum" : "Flambagem Local",
                TaxaUtilizacao = taxa,
                Observacao = $"Fator de reducao local: {fatorReducao:F3}. Area efetiva: {areaEfetivaMm2:F1} mm2."
            };
        }

        /// <summary>
        /// Flambagem distorcional -- metodo simplificado (Schafer/Pekoz). ESTE E O PONTO DE MAIOR
        /// INCERTEZA do calculo: o metodo exato do Anexo C da NBR 14762 e mais elaborado. Trate este
        /// metodo como uma primeira aproximacao conservadora, nao como valor final de projeto.
        /// </summary>
        public ResultadoVerificacao VerificarFlambagemDistorcional(PerfilLSF perfil, double cargaAxialN)
        {
            double t = perfil.EspessuraMm;
            double h = perfil.AlturaAlmaMm;
            double fy = perfil.TensaoEscoamentoMPa;
            double E = perfil.ModuloElasticidadeMPa;

            // Coeficiente de flambagem distorcional simplificado -- aproximacao, CONFIRME contra
            // calculo exato (ou software de secao finita, ex: CUFSM) antes de uso em producao.
            const double kDist = 0.5;

            double fcr = kDist * Math.PI * Math.PI * E /
                         (12.0 * (1.0 - CoeficientePoisson * CoeficientePoisson)) *
                         Math.Pow(t / h, 2);

            double esbeltezReduzida = Math.Sqrt(fy / fcr);

            double fatorReducao;
            if (esbeltezReduzida <= 0.561)
            {
                fatorReducao = 1.0;
            }
            else
            {
                fatorReducao = (1.0 - 0.25 / Math.Pow(esbeltezReduzida, 1.2)) / Math.Pow(esbeltezReduzida, 1.2);
            }

            double areaEfetivaMm2 = perfil.AreaMm2 * fatorReducao;
            double cargaResistenteN = areaEfetivaMm2 * fy;

            double taxa = cargaAxialN / cargaResistenteN;

            return new ResultadoVerificacao
            {
                Aprovado = taxa <= 1.0,
                ModoCritico = taxa <= 1.0 ? "Nenhum" : "Flambagem Distorcional",
                TaxaUtilizacao = taxa,
                Observacao = $"[VALIDAR] Fator de reducao distorcional: {fatorReducao:F3}."
            };
        }

        /// <summary>
        /// Flambagem global (flexao) por Euler, com interacao flexo-compressao simplificada.
        /// </summary>
        public ResultadoVerificacao VerificarFlambagemGlobal(
            PerfilLSF perfil, double comprimentoDestravadoMm, double cargaAxialN, double momentoFletorNmm)
        {
            double E = perfil.ModuloElasticidadeMPa;
            double I = perfil.MomentoInerciaMm4;
            double fy = perfil.TensaoEscoamentoMPa;
            double A = perfil.AreaMm2;

            double cargaCriticaEulerN = Math.PI * Math.PI * E * I / Math.Pow(comprimentoDestravadoMm, 2);
            double cargaResistenteCompressaoN = Math.Min(cargaCriticaEulerN, A * fy);

            double moduloResistenciaMm3 = I / (perfil.AlturaAlmaMm / 2.0);
            double momentoResistenteNmm = moduloResistenciaMm3 * fy;

            // Interacao flexo-compressao simplificada (soma linear das taxas de utilizacao) --
            // NBR 14762 usa formula de interacao mais elaborada; esta e uma aproximacao conservadora.
            double taxaAxial = cargaAxialN / cargaResistenteCompressaoN;
            double taxaFletora = momentoFletorNmm / momentoResistenteNmm;
            double taxaCombinada = taxaAxial + taxaFletora;

            return new ResultadoVerificacao
            {
                Aprovado = taxaCombinada <= 1.0,
                ModoCritico = taxaCombinada <= 1.0 ? "Nenhum" : "Flambagem Global (flexo-compressao)",
                TaxaUtilizacao = taxaCombinada,
                Observacao = $"Taxa axial: {taxaAxial:F3}, taxa fletora: {taxaFletora:F3}."
            };
        }

        /// <summary>
        /// Verifica se a flecha calculada respeita o limite de servico L/300.
        /// </summary>
        public ResultadoVerificacao VerificarFlecha(double comprimentoMm, double flechaCalculadaMm)
        {
            double flechaLimiteMm = comprimentoMm / 300.0;
            double taxa = flechaCalculadaMm / flechaLimiteMm;

            return new ResultadoVerificacao
            {
                Aprovado = taxa <= 1.0,
                ModoCritico = taxa <= 1.0 ? "Nenhum" : "Flecha excedida (L/300)",
                TaxaUtilizacao = taxa,
                Observacao = $"Flecha limite: {flechaLimiteMm:F2} mm. Flecha calculada: {flechaCalculadaMm:F2} mm."
            };
        }
    }
}
