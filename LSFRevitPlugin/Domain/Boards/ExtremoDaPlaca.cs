using System;

namespace LSFRevitPlugin.Domain.Boards
{
    /// <summary>Que tipo de encontro existe na ponta de uma parede.</summary>
    public enum TipoEncontroNaPonta
    {
        Nenhum,

        /// <summary>Canto em L: a ponta encosta na ponta de outra parede.</summary>
        EmL,

        /// <summary>Encontro em T: a ponta chega no meio de outra parede, que passa.</summary>
        EmT
    }

    /// <summary>
    /// Onde a placa termina nos encontros entre paredes. Tudo em mm, medido ao longo da parede
    /// a partir da ponta inicial. Sem dependencia do Revit.
    ///
    /// Regra geral: em cada encontro uma parede tem a placa encostada no perfil (a estrutura)
    /// da outra, e a outra fica recuada pela espessura da placa da primeira.
    /// </summary>
    public static class ExtremoDaPlaca
    {
        /// <summary>Diferenca (mm) entre o fim da estrutura e a ponta da parede abaixo da qual a ponta e livre.</summary>
        public const double ToleranciaMm = 5.0;

        /// <param name="comprimentoMm">Comprimento da parede na linha de localizacao.</param>
        /// <param name="estruturaMm">Fim da estrutura nessa ponta (o menor U no inicio, o maior U no fim).</param>
        /// <param name="noInicio">true para a ponta inicial da parede, false para a final.</param>
        /// <param name="encontro">Tipo de encontro que existe nessa ponta.</param>
        /// <param name="faceInterna">So no L: true se a face da placa fica do lado de dentro do canto.</param>
        /// <param name="espessuraPlacaMm">Espessura da placa dessa face (usada tambem como a da outra parede).</param>
        /// <param name="espessuraEstruturaOutraMm">Espessura da estrutura da outra parede do encontro.</param>
        public static double Calcular(
            double comprimentoMm, double estruturaMm, bool noInicio, TipoEncontroNaPonta encontro,
            bool faceInterna, double espessuraPlacaMm, double espessuraEstruturaOutraMm)
        {
            double ponta = noInicio ? 0.0 : comprimentoMm;

            // Sentido para fora da parede: -1 na ponta inicial, +1 na final.
            double sinal = noInicio ? -1.0 : 1.0;

            // T: a parede que chega tem a placa encostada no perfil da parede que passa, ou seja,
            // ela vai ate a face da estrutura dela (metade da espessura antes do eixo). A parede
            // que passa e quem fica recuada (ver LarguraDoCorte).
            if (encontro == TipoEncontroNaPonta.EmT)
            {
                return ponta - sinal * (espessuraEstruturaOutraMm / 2.0);
            }

            if (encontro != TipoEncontroNaPonta.EmL) return ponta;

            // Positivo: a estrutura passa da ponta (parede que chega). Negativo: recua (parede que recebe).
            double avanco = (estruturaMm - ponta) * sinal;
            if (Math.Abs(avanco) <= ToleranciaMm) return ponta;

            if (avanco > 0)
            {
                // Chega: a face externa acaba rente ao fim da estrutura (a face do montante da outra
                // parede); a face interna para na face do montante da outra.
                return faceInterna ? ponta - sinal * avanco : estruturaMm;
            }

            // Recebe: a face externa passa a largura do outro montante mais a espessura da outra
            // placa, alem do fim da estrutura; a face interna para na placa da outra parede.
            return faceInterna
                ? estruturaMm - sinal * espessuraPlacaMm
                : estruturaMm + sinal * (espessuraEstruturaOutraMm + espessuraPlacaMm);
        }

        /// <summary>
        /// Largura do corte que uma parede abre na placa de outra que encosta ou cruza nela, centrado
        /// no eixo da outra. Se a placa desta parede encosta no perfil da outra, o corte e a espessura
        /// da estrutura dela; se fica recuada, o corte inclui as duas placas da outra (a espessura da
        /// placa de cada lado).
        /// </summary>
        public static double LarguraDoCorte(
            bool encostaNoPerfilDaOutra, double espessuraEstruturaOutraMm, double espessuraPlacaMm)
        {
            return encostaNoPerfilDaOutra
                ? espessuraEstruturaOutraMm
                : espessuraEstruturaOutraMm + 2.0 * espessuraPlacaMm;
        }
    }
}