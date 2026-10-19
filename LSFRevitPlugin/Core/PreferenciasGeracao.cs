using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Guarda e recupera a ultima escolha de perfil/espacamento feita pelo usuario, para que
    /// a janela de opcoes ja abra pre-preenchida na proxima parede (evita reconfigurar toda vez
    /// para paredes semelhantes). Persistencia simples em arquivo texto por usuario/maquina,
    /// nao vinculada a um projeto Revit especifico.
    /// </summary>
    public static class PreferenciasGeracao
    {
        private static readonly string CaminhoArquivo = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LSFRevitPlugin", "ultima_selecao.txt");

        public static (PerfilSpec Perfil, double EspacamentoCm) CarregarUltimaSelecaoOuPadrao()
        {
            try
            {
                if (File.Exists(CaminhoArquivo))
                {
                    string[] linhas = File.ReadAllLines(CaminhoArquivo);
                    if (linhas.Length >= 4)
                    {
                        SistemaPerfil sistema = linhas[0] == "Drywall"
                            ? SistemaPerfil.Drywall
                            : SistemaPerfil.SteelFrame;
                        double largura = double.Parse(linhas[1], CultureInfo.InvariantCulture);
                        double bitola = double.Parse(linhas[2], CultureInfo.InvariantCulture);
                        double espacamento = double.Parse(linhas[3], CultureInfo.InvariantCulture);

                        return (new PerfilSpec
                        {
                            Sistema = sistema,
                            LarguraAlmaMm = largura,
                            EspessuraChapaMm = bitola
                        }, espacamento);
                    }
                }
            }
            catch
            {
                // Arquivo corrompido, sem permissao, etc. -- ignora e usa o padrao.
            }

            return (PerfilSpec.CriarPadrao(), 40.0);
        }

        public static void Salvar(PerfilSpec perfil, double espacamentoCm)
        {
            try
            {
                string pasta = Path.GetDirectoryName(CaminhoArquivo);
                if (!string.IsNullOrEmpty(pasta) && !Directory.Exists(pasta))
                {
                    Directory.CreateDirectory(pasta);
                }

                string conteudo = string.Join(Environment.NewLine, new[]
                {
                    perfil.Sistema.ToString(),
                    perfil.LarguraAlmaMm.ToString(CultureInfo.InvariantCulture),
                    perfil.EspessuraChapaMm.ToString(CultureInfo.InvariantCulture),
                    espacamentoCm.ToString(CultureInfo.InvariantCulture)
                });

                File.WriteAllText(CaminhoArquivo, conteudo);
            }
            catch
            {
                // Falha ao salvar preferencia nao deve interromper o fluxo de geracao.
            }
        }

        public static bool BlockingHabilitado { get; set; }
        public static int BlockingFileiras { get; set; } = 1;

        /// <summary>Finalidade das fileiras de blocking (travamento, apoio das placas ou ambas).</summary>
        public static FinalidadeBlocking BlockingFinalidade { get; set; } = FinalidadeBlocking.Ambas;

        /// <summary>
        /// Alturas independentes das fileiras de blocking, em cm a partir da base da parede,
        /// na ordem informada pelo usuario. Vazio = usar fileiras automaticas ou o alinhamento
        /// as juntas das placas (quando solicitado e houver dados reais).
        /// </summary>
        public static List<double> BlockingAlturasCm { get; set; } = new List<double>();

        /// <summary>Quando true, tenta alinhar as fileiras as juntas horizontais reais das placas.</summary>
        public static bool BlockingAlinharJuntas { get; set; }

        /// <summary>Distribuicao das fileiras de blocking (todos os vaos ou alternados).</summary>
        public static DistribuicaoBlocking BlockingDistribuicao { get; set; } = DistribuicaoBlocking.Todos;

        /// <summary>Inverte o montante inicial da distribuicao alternada.</summary>
        public static bool BlockingAlternarInvertido { get; set; }

        /// <summary>
        /// Monta um <see cref="BlockingConfig"/> com as preferencias atuais. As alturas das juntas
        /// das placas NAO sao preenchidas aqui: quem tem acesso ao projeto (a geracao) le as juntas
        /// reais e as informa, para o plugin nunca presumir alturas sem dados das placas.
        /// </summary>
        public static BlockingConfig CriarBlockingConfig()
        {
            return new BlockingConfig
            {
                Habilitado = BlockingHabilitado,
                Fileiras = BlockingFileiras,
                Finalidade = BlockingFinalidade,
                AlturasRelativasCm = new List<double>(BlockingAlturasCm ?? new List<double>()),
                AlinharJuntasPlacas = BlockingAlinharJuntas,
                Distribuicao = BlockingDistribuicao,
                AlternarInvertido = BlockingAlternarInvertido
            };
        }
    }
}