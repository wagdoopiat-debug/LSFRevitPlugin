using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;

namespace LSFRevitPlugin.Services
{
    public static class BlockingService
    {
        /// <summary>
        /// Gera os travamentos horizontais (blocking) de uma parede e devolve quantos elementos
        /// foram efetivamente criados. Qualquer motivo de nao gerar (config desabilitada, altura
        /// invalida, solido invalido, excecao) e registrado em <paramref name="avisos"/>.
        /// </summary>
        public static int Gerar(
            Document doc,
            Wall parede,
            XYZ direcaoPrincipal,
            XYZ inicioParede,
            List<(XYZ Ponto, bool EhExtremidade, bool EhFimDoPainel, bool EhPontaDaParede, int Painel)> pontosGrade,
            List<OpeningInfo> vaos,
            BlockingConfig config,
            PerfilSpec perfil,
            ElementId matBlocking,
            Func<XYZ, int> painelNaPosicao,
            Action<DirectShape, string, int> marcar,
            List<string> avisos = null)
        {
            if (doc == null || parede == null || config == null || perfil == null)
            {
                if (avisos != null) avisos.Add("Blocking: dados incompletos (doc/parede/config/perfil nulos).");
                return 0;
            }

            config.Validar();
            if (!config.Habilitado)
            {
                if (avisos != null) avisos.Add("Blocking: desabilitado.");
                return 0;
            }
            // Fileiras automaticas podem ser 0 quando o usuario define alturas explicitas ou
            // quando manda alinhar as juntas das placas; nesses casos ainda ha o que gerar.
            bool temAlturasExplicitas = config.AlturasRelativasCm != null && config.AlturasRelativasCm.Count > 0;
            bool temJuntasParaAlinhar = config.AlinharJuntasPlacas
                && config.AlturasJuntasPlacasCm != null && config.AlturasJuntasPlacasCm.Count > 0;
            if (config.Fileiras <= 0 && !temAlturasExplicitas && !temJuntasParaAlinhar)
            {
                if (avisos != null) avisos.Add("Blocking: nenhuma fileira configurada (defina alturas, fileiras ou alinhe às juntas).");
                return 0;
            }

            int criados = 0;
            try
            {
                var blocos = BlockingCalculator.Calcular(parede, direcaoPrincipal, inicioParede, pontosGrade, vaos, config, perfil, painelNaPosicao, null, avisos);
                if (avisos != null) avisos.Add("Blocking: " + blocos.Count + " bloco(s) calculado(s).");
                if (blocos.Count == 0) return 0;

                // O blocking e uma GUIA (track) em "U" -- nao um montante. Ao encontrar cada montante, a SECAO
                // INTEIRA da guia (ALMA E AS DUAS ABAS JUNTAS) e cortada e dobrada 90 graus PARA BAIXO,
                // formando uma perna vertical que abraca o montante; a linha de dobra fica colada na
                // face do montante, o CORPO (alma + abas, secao "U" integra) vai de uma linha de dobra
                // a outra e cada perna e a continuacao da propria secao (boca da "U" voltada para o
                // montante). O vinco/raio de dobra, o entalhe de alivio (canto entre alma e aba na
                // regiao dobrada) e a fixacao NAO estao validados; profundidade provisoria.
                if (avisos != null)
                    avisos.Add("Blocking: guia \"U\" com a SECAO INTEIRA (alma + abas) dobrada 90 graus PARA BAIXO na face de cada montante, formando perna vertical que abraca o montante (corpo horizontal integro entre as dobras); vinco/raio, entalhe de alivio e fixacao NAO validados para fabricacao.");

                if (avisos != null)
                    avisos.Add(config.Distribuicao == DistribuicaoBlocking.Alternado
                        ? "Blocking: distribuicao ALTERNADA (vaos alternados; montante inicial " + (config.AlternarInvertido ? "invertido)." : "normal).")
                        : "Blocking: distribuicao em TODOS os vaos entre montantes adjacentes.");

                XYZ dirEixoParede = direcaoPrincipal.Normalize();
                XYZ nor = new XYZ(-dirEixoParede.Y, dirEixoParede.X, 0.0).Normalize();

                foreach (var b in blocos)
                {
                    if (b.PontoInicial == null || b.PontoFinal == null)
                    {
                        if (avisos != null) avisos.Add("Blocking: posicao do bloco sem extremidades definidas (painel " + b.Painel + ").");
                        continue;
                    }

                    List<Solid> solidos = BlockingTrackBuilder.CriarSolidosTrackBlocking(
                        b.PontoInicial, b.DirecaoSpanLong, nor, b.ComprimentoAoLongoEixo,
                        perfil, matBlocking);
                    if (solidos == null || solidos.Count == 0)
                    {
                        if (avisos != null) avisos.Add("Blocking: sólido inválido (bloco no painel " + b.Painel + ").");
                        continue;
                    }

                    DirectShape ds = StudDirectShapeFactory.CriarElementoMontante(doc, solidos, "LSF-Blocking-H");
                    if (ds == null)
                    {
                        if (avisos != null) avisos.Add("Blocking: falha ao criar DirectShape (bloco no painel " + b.Painel + ").");
                        continue;
                    }

                    marcar?.Invoke(ds, "Blocking horizontal (guia cortada/dobrada)", b.Painel);
                    criados++;
                }

                if (avisos != null) avisos.Add("Blocking: " + criados + " elemento(s) criado(s).");
            }
            catch (Exception ex)
            {
                if (avisos != null) avisos.Add("Blocking: erro ao gerar (" + ex.GetType().Name + ": " + ex.Message + ").");
            }

            return criados;
        }
    }
}
