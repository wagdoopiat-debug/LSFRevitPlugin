using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.UI
{
    /// <summary>
    /// Janela simples para escolher qual laje registrada editar/regenerar. Feita em codigo (sem
    /// XAML) de proposito: e uma lista curta e descartavel. Devolve o registro escolhido em
    /// <see cref="Selecionado"/> quando DialogResult for true.
    /// </summary>
    public sealed class EscolhaLajeWindow : Window
    {
        private sealed class Item
        {
            public RegistroLaje Registro;

            public override string ToString()
            {
                ParametrosLaje p = Registro.Parametros;
                string area = p?.Contorno != null
                    ? p.Contorno.AreaM2.ToString("F2", CultureInfo.InvariantCulture)
                    : "?";
                string espacamento = p != null
                    ? p.EspacamentoMm.ToString("F0", CultureInfo.InvariantCulture)
                    : "?";

                return "Laje " + Registro.LajeId
                    + "  -  " + TipoOrigemContornoTexto.Nome(Registro.OrigemTipo)
                    + " - " + area + " m2 - esp. " + espacamento + " mm";
            }
        }

        private readonly ListBox _lista = new ListBox { Height = 150 };

        /// <summary>Laje escolhida. So valida se DialogResult for true.</summary>
        public RegistroLaje Selecionado { get; private set; }

        public EscolhaLajeWindow(IEnumerable<RegistroLaje> registros)
        {
            Title = "LSF Modeler - Escolher laje";
            Width = 430;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var raiz = new StackPanel { Margin = new Thickness(16) };
            raiz.Children.Add(new TextBlock
            {
                Text = "Escolha a laje para editar/regenerar:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (registros != null)
            {
                foreach (RegistroLaje r in registros)
                {
                    _lista.Items.Add(new Item { Registro = r });
                }
            }

            if (_lista.Items.Count > 0) _lista.SelectedIndex = 0;
            raiz.Children.Add(_lista);

            var botoes = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var btnEditar = new Button
            {
                Content = "Editar",
                Width = 80,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnEditar.Click += BtnEditar_Click;

            var btnCancelar = new Button { Content = "Cancelar", Width = 80, IsCancel = true };

            botoes.Children.Add(btnEditar);
            botoes.Children.Add(btnCancelar);
            raiz.Children.Add(botoes);

            Content = raiz;
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            var item = _lista.SelectedItem as Item;
            if (item == null) return;

            Selecionado = item.Registro;
            DialogResult = true;
            Close();
        }
    }
}
