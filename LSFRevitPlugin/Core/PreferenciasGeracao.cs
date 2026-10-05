using System;
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
    }
}