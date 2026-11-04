using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Codec de texto da configuracao de uma laje (ParametrosLaje), em formato chave=valor por
    /// linha, cultura invariante. Serve para guardar a configuracao usada e poder reabrir a janela
    /// pre-preenchida e regenerar sem pedir tudo de novo. Nao guarda o catalogo nem a geometria
    /// gerada, apenas as escolhas do usuario + o contorno.
    ///
    /// Puro (sem Revit), para ser testado sem abrir o Revit.
    /// </summary>
    public static class ConfiguracaoLajeTexto
    {
        public const int VersaoAtual = 1;

        public static string Serializar(ParametrosLaje p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));

            var linhas = new List<string>
            {
                "versao=" + VersaoAtual.ToString(CultureInfo.InvariantCulture),
                "tipo=" + p.Tipo,
                "sistema=" + p.Sistema,
                "tipoViga=" + p.TipoViga,
                "trelica=" + p.Trelica,
                "montagemTrelica=" + p.MontagemTrelica,
                "secaoBanzo=" + p.SecaoBanzo,
                "gussetPlacas=" + p.GussetPlacas,
                "composicaoCaixao=" + p.ComposicaoCaixao,
                "alturaTrelica=" + Num(p.AlturaTrelicaMm),
                "paineisTrelica=" + (p.NumeroPaineisTrelica.HasValue
                    ? p.NumeroPaineisTrelica.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty),
                "painelAlvoTrelica=" + Num(p.PainelTrelicaAlvoMm),
                "perfil.sistema=" + (p.Perfil != null ? p.Perfil.Sistema.ToString() : SistemaPerfil.SteelFrame.ToString()),
                "perfil.largura=" + Num(p.Perfil != null ? p.Perfil.LarguraAlmaMm : 0.0),
                "perfil.bitola=" + Num(p.Perfil != null ? p.Perfil.EspessuraChapaMm : 0.0),
                "espacamento=" + Num(p.EspacamentoMm),
                "cota=" + Num(p.CotaMm),
                "telha=" + Num(p.EspessuraTelhaMm),
                "composicao=" + p.ComposicaoPlacas,
                "osb=" + Num(p.EspessuraOSBMm),
                "cimenticia=" + Num(p.EspessuraCimenticiaMm),
                "fixadores=" + p.NivelFixadores,
                "espFixadores=" + Num(p.EspacamentoFixadoresPlacaMm),
                "preencherOndas=" + (p.PreencherOndas ? "1" : "0"),
                "fitasTensionadas=" + (p.FitasTensionadas ? "1" : "0"),
                "espFitas=" + Num(p.EspacamentoFitasTensionadasMm),
                "modo=" + p.ModoOrientacao,
                "aresta=" + p.ArestaReferencia.ToString(CultureInfo.InvariantCulture),
                "graus=" + (p.DirecaoVigasGraus.HasValue ? Num(p.DirecaoVigasGraus.Value) : string.Empty),
                "contorno=" + SerializarContorno(p.Contorno)
            };

            return string.Join("\n", linhas);
        }

        /// <summary>
        /// Le a configuracao. Devolve false quando o texto esta vazio, mal formado, com versao
        /// desconhecida ou sem contorno valido.
        /// </summary>
        public static bool TentarDeserializar(string texto, out ParametrosLaje parametros)
        {
            parametros = null;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            Dictionary<string, string> campos = LerCampos(texto);

            string versaoTexto;
            int versao;
            if (!campos.TryGetValue("versao", out versaoTexto)
                || !int.TryParse(versaoTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out versao)
                || versao != VersaoAtual)
            {
                return false;
            }

            string contornoTexto;
            if (!campos.TryGetValue("contorno", out contornoTexto)) return false;

            ContornoLaje contorno = DesserializarContorno(contornoTexto);
            if (contorno == null || !contorno.EhValido) return false;

            var p = new ParametrosLaje { Contorno = contorno };

            TipoLaje tipo;
            if (campos.TryGetValue("tipo", out string tipoTexto) && Enum.TryParse(tipoTexto, out tipo))
                p.Tipo = tipo;

            SistemaLaje sistema;
            if (campos.TryGetValue("sistema", out string sistemaTexto) && Enum.TryParse(sistemaTexto, out sistema))
                p.Sistema = sistema;

            TipoVigaLaje tipoViga;
            if (campos.TryGetValue("tipoViga", out string tipoVigaTexto) && Enum.TryParse(tipoVigaTexto, out tipoViga))
                p.TipoViga = tipoViga;

            TrelicaTipo trelica;
            if (campos.TryGetValue("trelica", out string trelicaTexto) && Enum.TryParse(trelicaTexto, out trelica))
                p.Trelica = trelica;

            // Configs antigas sem estas chaves mantem o padrao (Encaixada / FrenteAFrente).
            MontagemTrelica montagemTrelica;
            if (campos.TryGetValue("montagemTrelica", out string montagemTexto)
                && Enum.TryParse(montagemTexto, out montagemTrelica))
                p.MontagemTrelica = montagemTrelica;

            SecaoBanzoTrelica secaoBanzo;
            if (campos.TryGetValue("secaoBanzo", out string secaoBanzoTexto)
                && Enum.TryParse(secaoBanzoTexto, out secaoBanzo))
                p.SecaoBanzo = secaoBanzo;

            bool gusset;
            if (campos.TryGetValue("gussetPlacas", out string gussetTexto)
                && bool.TryParse(gussetTexto, out gusset))
                p.GussetPlacas = gusset;

            ComposicaoCaixaoTipo composicaoCaixao;
            if (campos.TryGetValue("composicaoCaixao", out string caixaoTexto)
                && Enum.TryParse(caixaoTexto, out composicaoCaixao))
                p.ComposicaoCaixao = composicaoCaixao;

            p.AlturaTrelicaMm = LerDouble(campos, "alturaTrelica", ComposicaoTrelica.AlturaPadraoMm);
            p.PainelTrelicaAlvoMm = LerDouble(campos, "painelAlvoTrelica", ComposicaoTrelica.PainelAlvoPadraoMm);

            string paineisTexto;
            int paineis;
            if (campos.TryGetValue("paineisTrelica", out paineisTexto)
                && !string.IsNullOrEmpty(paineisTexto)
                && int.TryParse(paineisTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out paineis))
            {
                p.NumeroPaineisTrelica = paineis;
            }

            SistemaPerfil perfilSistema = SistemaPerfil.SteelFrame;
            string perfilSistemaTexto;
            if (campos.TryGetValue("perfil.sistema", out perfilSistemaTexto))
                Enum.TryParse(perfilSistemaTexto, out perfilSistema);

            p.Perfil = new PerfilSpec
            {
                Sistema = perfilSistema,
                LarguraAlmaMm = LerDouble(campos, "perfil.largura", perfilSistema == SistemaPerfil.Drywall ? 48.0 : 90.0),
                EspessuraChapaMm = LerDouble(campos, "perfil.bitola", perfilSistema == SistemaPerfil.Drywall ? 0.50 : 0.95)
            };

            p.EspacamentoMm = LerDouble(campos, "espacamento", 400.0);
            p.CotaMm = LerDouble(campos, "cota", 0.0);
            p.EspessuraTelhaMm = LerDouble(campos, "telha", TelhaTp40.Espessura043Mm);

            ComposicaoTipo composicao;
            if (campos.TryGetValue("composicao", out string composicaoTexto)
                && Enum.TryParse(composicaoTexto, out composicao))
            {
                p.ComposicaoPlacas = composicao;
            }

            p.EspessuraOSBMm = LerDouble(campos, "osb", 15.0);
            p.EspessuraCimenticiaMm = LerDouble(campos, "cimenticia", 10.0);

            NivelDetalheFixadores fixadores;
            if (campos.TryGetValue("fixadores", out string fixadoresTexto)
                && Enum.TryParse(fixadoresTexto, out fixadores))
            {
                p.NivelFixadores = fixadores;
            }

            p.EspacamentoFixadoresPlacaMm =
                LerDouble(campos, "espFixadores", FixadoresLaje.EspacamentoPlacaPadraoMm);

            // Obs.: "paginar" e "juntaPlacas" existiam em versoes antigas; a paginacao das placas
            // passou a ser sempre aplicada (junta fixa em PaginacaoPlacas.JuntaPadraoMm), portanto
            // essas chaves antigas sao simplesmente ignoradas -- config antiga continua abrindo.

            string preencherOndasTexto;
            if (campos.TryGetValue("preencherOndas", out preencherOndasTexto))
                p.PreencherOndas = preencherOndasTexto != "0";

            string fitasTexto;
            if (campos.TryGetValue("fitasTensionadas", out fitasTexto))
                p.FitasTensionadas = fitasTexto != "0";

            p.EspacamentoFitasTensionadasMm = LerDouble(
                campos, "espFitas", FitasTensionadasLaje.PassoPadraoMm);

            OrientacaoVigasModo modo;
            if (campos.TryGetValue("modo", out string modoTexto) && Enum.TryParse(modoTexto, out modo))
                p.ModoOrientacao = modo;

            string arestaTexto;
            int aresta;
            if (campos.TryGetValue("aresta", out arestaTexto)
                && int.TryParse(arestaTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out aresta))
            {
                p.ArestaReferencia = aresta;
            }

            string grausTexto;
            if (campos.TryGetValue("graus", out grausTexto) && !string.IsNullOrEmpty(grausTexto))
            {
                double graus;
                if (double.TryParse(grausTexto, NumberStyles.Float, CultureInfo.InvariantCulture, out graus))
                    p.DirecaoVigasGraus = graus;
            }

            parametros = p;
            return true;
        }

        private static Dictionary<string, string> LerCampos(string texto)
        {
            var campos = new Dictionary<string, string>();
            foreach (string linha in texto.Split('\n'))
            {
                string l = linha.TrimEnd('\r');
                if (l.Length == 0) continue;

                int igual = l.IndexOf('=');
                if (igual <= 0) continue;

                string chave = l.Substring(0, igual);
                string valor = l.Substring(igual + 1);
                campos[chave] = valor;
            }

            return campos;
        }

        private static string SerializarContorno(ContornoLaje contorno)
        {
            if (contorno == null) return string.Empty;

            var partes = new List<string>();
            foreach (Ponto2DMm p in contorno.Pontos)
            {
                partes.Add(Num(p.XMm) + "," + Num(p.YMm));
            }

            return string.Join(";", partes);
        }

        private static ContornoLaje DesserializarContorno(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            var pontos = new List<Ponto2DMm>();
            foreach (string par in texto.Split(';'))
            {
                if (par.Length == 0) continue;

                string[] xy = par.Split(',');
                if (xy.Length != 2) return null;

                double x;
                double y;
                if (!double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return null;
                if (!double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return null;

                pontos.Add(new Ponto2DMm(x, y));
            }

            if (pontos.Count < 3) return null;

            try
            {
                return new ContornoLaje(pontos);
            }
            catch
            {
                return null;
            }
        }

        private static double LerDouble(Dictionary<string, string> campos, string chave, double padrao)
        {
            string texto;
            double valor;
            if (campos.TryGetValue(chave, out texto)
                && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor))
            {
                return valor;
            }

            return padrao;
        }

        private static string Num(double valor)
        {
            return valor.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
