# LSF Modeler — plugin Revit para Light Steel Frame e Drywall

Plugin para o **Autodesk Revit 2021** (C#, .NET Framework 4.8) que automatiza o projeto de paredes em *light steel frame* e *drywall* a partir de paredes comuns do Revit.

O que ele faz:

- gera a **estrutura** da parede: montantes, guias, reforços de vão (King, Jack, verga, contra-verga e cripple studs) e reforços dos encontros em L, T e +;
- divide a parede em **painéis** e gera a estrutura por painel;
- pagina as **placas de fechamento** (cimentícia, Glasroc X, OSB e gessos) por painel e por face, com juntas sobre montantes, recortes de portas e janelas e reaproveitamento de sobras no projeto inteiro;
- calcula o **quantitativo** (estrutura, lista de corte, placas, recortes e parafusos) numa janela dentro do Revit e em CSV;
- cria os **parafusos em 3D**.

> **Aviso.** Este projeto é uma ferramenta de apoio ao projeto e à quantificação. Alguns coeficientes da NBR 14762 e algumas regras de fixação de placas (OSB e Glasroc X) estão marcados como `[VALIDAR]` no código, por não terem fonte direta. Confira todo resultado com um profissional habilitado antes de usá-lo em obra.

---

## Sumário

1. [Requisitos](#1-requisitos)
2. [Obter o projeto](#2-obter-o-projeto)
3. [Compilar](#3-compilar)
4. [Instalar no Revit](#4-instalar-no-revit)
5. [Rodar os testes](#5-rodar-os-testes)
6. [Guia de uso](#6-guia-de-uso)
7. [Regras de projeto embutidas](#7-regras-de-projeto-embutidas)
8. [Códigos e etiquetas](#8-códigos-e-etiquetas)
9. [Arquivos gravados no seu computador](#9-arquivos-gravados-no-seu-computador)
10. [Estrutura do repositório](#10-estrutura-do-repositório)
11. [Documentos do projeto](#11-documentos-do-projeto)
12. [Desenvolvimento](#12-desenvolvimento)
13. [Problemas comuns](#13-problemas-comuns)
14. [Estado do projeto](#14-estado-do-projeto)
15. [Glossário](#15-glossário)
16. [Referências](#16-referências)
17. [Licença](#17-licença)

---

## 1. Requisitos

| Item | Detalhe |
|---|---|
| Sistema operacional | Windows 10 ou 11, 64 bits |
| Autodesk Revit | **2021** (a única versão testada) |
| .NET SDK | qualquer versão recente, para usar o comando `dotnet` |
| .NET Framework 4.8 | pacote de destino (*targeting pack* / *Developer Pack*); já vem com o Visual Studio 2022 |
| Editor | Visual Studio 2022 (Community serve) ou VS Code |

O projeto compila para `net48`, `x64`, com `LangVersion 8.0` e WPF habilitado (`UseWPF`). Ele referencia `RevitAPI.dll` e `RevitAPIUI.dll` da instalação do Revit, por padrão em:

```
C:\Program Files\Autodesk\Revit 2021\
```

Se o seu Revit estiver em outra pasta, veja a [seção 3](#3-compilar).

---

## 2. Obter o projeto

O projeto não está publicado em nenhum servidor git. Receba a pasta do projeto (cópia ou `.zip`) e deixe-a em:

```
C:\lsf_revit_plugin
```

Esse é o caminho que o arquivo `LSFRevitPlugin.addin` já usa. Se você colocar a pasta em outro lugar, ajuste a tag `<Assembly>` do `.addin` (veja a [seção 4](#4-instalar-no-revit)).

Se o projeto veio de um `.zip` baixado, o Windows pode bloquear os arquivos. Antes de extrair, clique com o botão direito no `.zip`, escolha **Propriedades** e marque **Desbloquear**.

Se quiser versionar localmente com git:

```powershell
cd C:\lsf_revit_plugin
git init
```

Neste caso, crie um `.gitignore` que ignore as pastas `bin`, `obj` e `.vs`.

---

## 3. Compilar

> **Feche o Revit antes de compilar.** Enquanto o Revit estiver aberto com o plugin carregado, o Windows trava a DLL e o build falha.

Na raiz do projeto, no PowerShell:

```powershell
cd C:\lsf_revit_plugin
dotnet build LSFRevitPlugin.sln
```

Isso compila o plugin e o projeto de testes. Para compilar só o plugin:

```powershell
dotnet build LSFRevitPlugin\LSFRevitPlugin.csproj
```

A DLL sai em:

```
C:\lsf_revit_plugin\LSFRevitPlugin\bin\Debug\net48\LSFRevitPlugin.dll
```

Para gerar em Release, use `-c Release`; a DLL sai em `bin\Release\net48\` e a tag `<Assembly>` do `.addin` precisa apontar para ela.

### Revit instalado em outra pasta

| Onde | O que ajustar |
|---|---|
| `LSFRevitPlugin\LSFRevitPlugin.csproj` | os dois `HintPath` (`RevitAPI.dll` e `RevitAPIUI.dll`) |
| `Tests\LSFRevitPlugin.Domain.Tests\LSFRevitPlugin.Domain.Tests.csproj` | a propriedade `RevitApiDir`, ou passe `-p:RevitApiDir="D:\Autodesk\Revit 2021"` na linha de comando |
| `rodar-testes.ps1` | a variável `$revit` no início do arquivo |

---

## 4. Instalar no Revit

O Revit descobre o plugin por um manifesto `.addin`. O repositório já traz um, na raiz: `LSFRevitPlugin.addin`.

1. Copie `LSFRevitPlugin.addin` para:

```
   %APPDATA%\Autodesk\Revit\Addins\2021\
```

2. Se o projeto não estiver em `C:\lsf_revit_plugin`, edite a tag `<Assembly>` da **cópia** para o caminho real da DLL.

3. Abra o Revit. Na primeira vez ele pergunta se deve carregar o plugin: escolha **Sempre carregar** (ou **Carregar uma vez**).

4. A aba **LSF Modeler** aparece na faixa de opções, com o painel **Estrutura** e quatro botões: **Ativar LSF**, **Painelizar paredes**, **Placas** e **Quantitativo**.

Conteúdo do manifesto:

```xml
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>LSF Modeler</Name>
    <Assembly>C:\lsf_revit_plugin\LSFRevitPlugin\bin\Debug\net48\LSFRevitPlugin.dll</Assembly>
    <AddInId>47de0149-1014-4630-9744-5944ef89d175</AddInId>
    <FullClassName>LSFRevitPlugin.Core.App</FullClassName>
    <VendorId>LSF</VendorId>
    <VendorDescription>Projeto LSF Modeler</VendorDescription>
  </AddIn>
</RevitAddIns>
```

O `AddInId` deve ser único por plugin instalado. Se você mantiver duas cópias do plugin lado a lado (por exemplo, uma de teste), gere um GUID novo para a segunda com `[guid]::NewGuid()` no PowerShell.

---

## 5. Rodar os testes

Os testes unitários ficam em `Tests\LSFRevitPlugin.Domain.Tests` (xUnit). Eles cobrem a paginação das placas (juntas, ancoragem, alocação de sobras, preparo da entrada), o cálculo estrutural da NBR 14762, o extremo da placa e os cripple studs.

Parte dos testes usa tipos da API do Revit, e o .NET precisa achar as DLLs nativas dele. O script `rodar-testes.ps1` coloca a pasta do Revit no `PATH` só daquela execução e roda `dotnet test`. Feche o Revit e, na raiz do projeto:

```powershell
cd C:\lsf_revit_plugin
Set-ExecutionPolicy -Scope Process Bypass
.\rodar-testes.ps1
```

O `Set-ExecutionPolicy -Scope Process Bypass` vale só para essa janela do PowerShell.

A suíte tem **56 testes** e todos devem passar. Na última execução: total 56, aprovados 56, falhas 0, ignorados 0, em cerca de 7 segundos. O resultado termina com um resumo com o total, os aprovados, os que falharam e os ignorados. Se algum falhar, o nome do teste e a mensagem de erro aparecem acima desse resumo.

Os testes de paginação fixam os próprios valores de entrada (largura mínima de peça e outros), então não dependem dos padrões do código.

---

## 6. Guia de uso

### Preparação

- Trabalhe numa **cópia** do projeto Revit no primeiro teste.
- Use **paredes retas**. Paredes curvas ainda não são suportadas.
- Em cada parede, a **Linha de Localização** deve ser **"Linha de centro da parede"**. Os cálculos de eixo e de encontros dependem disso.
- Os encontros usam a espessura do **núcleo estrutural** da parede, não a espessura total.

### Fluxo recomendado

1. **Ativar LSF** em cada parede (gera a estrutura).
2. Opcional: **Painelizar paredes**; depois, gere de novo a estrutura das paredes painelizadas com **Ativar LSF**.
3. **Placas** (gera as placas dos painéis).
4. **Quantitativo** (confere os números, grava o CSV e cria os parafusos em 3D).

### Botão Ativar LSF

1. Clique em **Ativar LSF** e selecione uma parede reta.
2. O plugin detecta as portas e janelas e abre a janela **Opções de Geração**:

   | Campo | Opções |
   |---|---|
   | Sistema | Steel Frame ou Drywall |
   | Largura do perfil (alma) | Steel Frame: 90, 140 ou 200 mm. Drywall: 48, 70 ou 90 mm |
   | Bitola (espessura da chapa) | Steel Frame: 0,80, 0,95 ou 1,25 mm. Drywall: 0,50 mm (fixo) |
   | Espaçamento entre montantes | 40 ou 60 cm |
   | Vãos da parede | botão **Ajustar vãos...** (só aparece quando a parede tem portas ou janelas) |

3. Em **Ajustar vãos...** você corrige as medidas de cada abertura (largura, soleira e topo). O ajuste vale só para aquela geração. Reforços, jacks e cripple studs seguem as medidas ajustadas.
4. Clique em **Gerar**. Ao final, uma janela mostra o resumo: montantes criados (principais, H, triplo, quádruplo), encontros L, T e +, vãos detectados, guias, perfil usado, painéis e o **maior vão entre montantes**, com aviso se ele passar do espaçamento escolhido.

A janela abre já preenchida com a **última escolha** de sistema, largura, bitola e espaçamento (veja a [seção 9](#9-arquivos-gravados-no-seu-computador)).

Perfis considerados:

| Sistema | Alma (mm) | Aba (mm) | Lábio (mm) | Chapa (mm) |
|---|---|---|---|---|
| Drywall | 48 / 70 / 90 | 35 | 7 | 0,50 |
| Steel Frame (Ue) | 90 / 140 / 200 | 40 | 12 | 0,80 / 0,95 / 1,25 |

Comportamentos importantes:

- **Gerar de novo numa parede já lançada apaga e refaz a estrutura inteira dela**, num único grupo de transações: um Ctrl+Z desfaz a limpeza e a geração.
- Montantes e guias recebem cores por sistema e por função: Drywall em tons de azul, Steel Frame em tons de laranja.
- A guia inferior é interrompida nos vãos de **porta**; janelas não interrompem a guia.
- Montantes da grade que ficam a menos de 4 cm de um reforço (canto, T, +, King, Jack) são pulados.
- Nos cantos em L, a guia da parede que chega avança até o lado de fora da outra, e a outra recebe o desconto de meia espessura.
- Onde dois montantes se encostam (par H do canto em L, encontro em +), eles ficam **de costas um para o outro**. Nas aberturas, a alma do perfil fica voltada para o vão.

### Botão Painelizar paredes

Divide uma parede em painéis, para a estrutura e as placas serem geradas painel a painel.

1. Clique em **Painelizar paredes** e selecione uma parede reta.
2. Se a parede já foi painelizada, uma janela oferece **Redefinir os painéis** ou **Remover a painelização** (a parede volta a ser um painel único). Se a painelização está desatualizada (o comprimento da parede mudou), ela é ignorada e você pode redefini-la.
3. Clique, na parede, em cada ponto onde um painel termina. Os pontos aceitam *snap* em extremidade, ponto médio, ponto mais próximo e interseção. A mensagem na barra mostra a distância do último ponto à ponta inicial.
4. O último painel vai até o fim da parede. **ESC** conclui.
5. Pontos muito perto de uma ponta da parede ou de outro ponto são ignorados (há uma largura mínima de painel).

O painel 1 começa na ponta inicial da parede (onde a linha de localização começou). Depois de painelizar, **gere de novo a estrutura da parede** com **Ativar LSF** para aplicar os painéis: cada painel recebe montantes de extremidade próprios, a grade de montantes recomeça no início de cada painel e as guias são cortadas nas divisas. Se uma divisa cai dentro de um vão, os montantes dessa divisa são omitidos.

### Botão Placas

Cria as placas de fechamento das paredes que **já têm estrutura gerada**. Se nenhuma parede tiver estrutura, o plugin avisa e não faz nada.

A janela **Placas** tem:

- uma linha por painel, mostrando parede, painel, largura em mm e se já tem placas ("com placas" ou "sem placas");
- a caixa **Refazer**: só os painéis marcados têm as placas (re)criadas; os desmarcados mantêm as que já têm. Painéis sem placas, ou com placas de uma versão antiga, já vêm marcados;
- por face (Face 1 e Face 2): **tipo**, **medida** e **orientação** (*Em pe* ou *Deitada*). Por padrão, Face 1 é Cimentícia e Face 2 é Gesso ST;
- no topo, uma linha de escolha única com o botão **Aplicar aos marcados**, que copia essas escolhas para todos os painéis marcados. Há também **Marcar todos** e **Desmarcar todos**;
- no rodapé, **Cadastrar nova medida**: escolha o tipo, informe os dois lados em mm e clique em **Cadastrar medida** (cada lado com pelo menos 100 mm e o maior com no máximo 6000 mm);
- os botões **Gerar placas** e **Cancelar**.

Ao terminar, um relatório mostra painéis refeitos e mantidos, peças criadas (quantas com recorte e quantas saíram de sobras), falhas, uma linha por tipo e medida ("N placas usadas, sobras guardadas X m²") e até 10 avisos.

Tipos de placa e medidas de fábrica:

| Tipo | Medidas de fábrica |
|---|---|
| Cimentícia, Glasroc X, OSB | 1,20 x 2,40 m |
| Gesso RU, Gesso ST, Gesso RF, Gesso Performa, Performa RU | 1,20 x 2,40 m e 1,20 x 1,80 m |

As medidas que você cadastra valem para o tipo escolhido e ficam salvas para as próximas vezes.

A face 1 fica do lado para onde a normal da parede aponta, encostada na estrutura; a face 2 fica do lado oposto.

Se, depois de gerar as placas, você mudar a estrutura, os vãos ou os encontros, o plugin avisa que as placas existentes não batem com a paginação atual. Marque **Refazer** nesse painel.

### Botão Quantitativo

Lê o que já está modelado e abre uma janela com abas. O cálculo pode levar alguns segundos, porque mede os parafusos na geometria das peças.

| Aba | Conteúdo |
|---|---|
| Estrutura | resumo por tipo e perfil, com quantidade e comprimento total |
| Corte | lista de corte da estrutura |
| Placas | placas de fábrica usadas, área líquida, área das placas e aproveitamento, por tipo e medida |
| Pecas | cada peça: código, parede, painel, face, tipo, medida, recortes, placa de origem, se foi reaproveitada e área |
| Recortes | cada recorte (largura x altura em mm) com a peça a que pertence |
| Parafusos | resumo por tipo de ligação e totais da estrutura, das placas e geral |
| Parafusos por painel | parafusos por parede e painel |
| Parafusos das placas | parafusos de cada peça de placa |
| Ligacoes | pares de montantes que se tocam e os parafusos de cada um (para conferência) |
| Criterios | as regras usadas no cálculo dos parafusos |
| Legenda | significado dos códigos |
| Paineis | lista de paredes e painéis, com contagem de elementos e peças |

Ações da janela:

- **Gerar CSV**: grava `LSF_Quantitativo_AAAAMMDD_HHMM.csv` (UTF-8, separador ponto e vírgula, números no padrão pt-BR) na pasta do projeto Revit, ou em *Documentos* se o projeto ainda não foi salvo, e abre o arquivo. O CSV tem as mesmas tabelas da janela.
- **Criar parafusos no 3D**: cria os parafusos como elementos de *Modelo genérico* (estrutura em vermelho, placas em preto). Cada parafuso tem corpo e cabeça saliente; há um elemento por tipo de ligação, parede e painel, chamado `LSF - Parafusos - <ligação> - P<painel>`. **Cada vez que você usa o botão, os parafusos 3D anteriores são apagados e recriados.** Se você mudar a estrutura ou as placas, abra o Quantitativo e crie os parafusos de novo.
- **Ctrl+C** em qualquer tabela copia as linhas selecionadas (com o cabeçalho) para o Excel.

---

## 7. Regras de projeto embutidas

### Placas

| Regra | Valor |
|---|---|
| Folga entre a base da placa e o piso | 15 mm, para todos os tipos |
| Juntas verticais | sempre sobre montantes, desencontradas entre fiadas vizinhas (desejável 400 mm ou mais) e entre as duas faces da parede |
| Distância mínima da junta à quina de uma abertura | 200 mm (padrão) |
| Largura mínima de peça vertical (faixa em pé entre juntas) | 400 mm, exceto quando uma porta, janela ou encontro está mais perto |
| Altura mínima de fiada | 200 mm |
| Reaproveitamento de sobras | no projeto inteiro: todas as faces são desenhadas e as peças são distribuídas da maior para a menor, usando a menor sobra em que cabem; só abre placa nova quando nenhuma serve. Sobras com um lado menor que 100 mm são perda |
| Recortes de porta e janela | usam a medida automática da esquadria |
| Junta no desenho 3D | os tipos com junta 0 (gessos e Glasroc X) ganham 3 mm de folga visível entre placas vizinhas; cimentícia (5 mm) e OSB (3 mm) já têm a junta nominal. O quantitativo usa as medidas nominais |

As larguras e distâncias mínimas são propriedades da entrada de paginação (`EntradaPaginacao`, em `Domain\Boards\Paginador.cs`).

Regras de encontro (placas):

- **Canto em L:** uma parede fica rente ao montante; a outra passa a largura do outro montante mais a espessura da placa.
- **T:** a parede que chega encosta no perfil da que passa; a que passa é interrompida só na face onde a outra chega (corte = estrutura + 2 placas).
- **+:** a parede de menor Id encosta no perfil; a de maior Id fica recuada pela espessura da placa; o corte vale para as duas faces.
- Nas pontas da parede, a placa acompanha o encontro; no meio, paredes que encostam ou cruzam interrompem a placa.

Regras de fixação por tipo (`Domain\Boards\PlacaCatalogo.cs`):

| Tipo | Espessura (mm) | Espaçamento ao longo do apoio (mm) | Distância da borda (mm) | Junta (mm) | Parafuso | Situação |
|---|---|---|---|---|---|---|
| Gessos (ST, RU, RF, Performa, Performa RU) | 12,5 | 250 | 10 | 0 | TA 25 mm | SINAPI / NBR 15758 |
| Cimentícia | 10 | 300 | 15 | 5 | 4,2 x 32 mm autobrocante | Manual Brasilit (rev. dez/2025) |
| Glasroc X | 12,5 | 250 | 10 | 0 | Glasroc 3,5 x 25 mm | **[VALIDAR]** (só o parafuso tem fonte) |
| OSB | 11,1 | 150 | 10 | 3 | 4,2 x 32 mm autobrocante | **[VALIDAR]** (valores de partida) |

### Parafusos

| Ligação | Regra |
|---|---|
| Montante na guia | 4 parafusos por montante vertical (principal, de divisa, H, triplo, T, quádruplo, King, Jack e cripple stud): 2 em cada ponta, um em cada mesa. Verga, contra-verga e guias não entram |
| Emenda costas com costas | autobrocante lentilha #8/#10 em zigue-zague ao longo da altura de contato dos dois montantes, passo de 250 mm, a 12,5 mm das bordas (faixas de projeto: 200 a 300 mm e 10 a 15 mm) |
| Conexão entre painéis | mesma regra, entre montantes de painéis diferentes da mesma parede |
| Conexão entre paredes | mesma regra, entre montantes de paredes diferentes (cantos em L, T e +) |
| Placas | uma linha de parafusos em cada montante da mesma parede que fica atrás da peça (dentro da mesa do perfil, onde a peça tem material, e a pelo menos a distância da borda das bordas), medida na geometria da peça e já descontando os recortes; peças apoiadas no piso ou no topo recebem também uma linha ao longo da guia. Espaçamento e distância da borda são os da regra do tipo de placa |

Observações:

- As ligações são achadas pela **geometria dos elementos modelados**: dois montantes verticais com folga de até 2 mm e pelo menos 100 mm de altura em comum.
- Cada ligação é contada uma só vez, no painel de menor número (e parede de menor Id).
- Os parafusos das placas só consideram montantes da própria parede; montantes de outra parede (cantos e encontros) não entram.
- O desenho 3D do parafuso é um prisma hexagonal (corpo e cabeça), só para marcar posição. O comprimento é o da regra do tipo de placa (estrutura: 13 mm).

---

## 8. Códigos e etiquetas

| Código | Significado |
|---|---|
| `<parede>-P<painel>-F<face>-<nn>` | Peça de placa. Ex.: `1234-P2-F1-03` = parede 1234 (Id do elemento no Revit), painel 2, face 1, peça 03 (ordem de leitura: fiada de baixo para cima, da esquerda para a direita). O mesmo código fica no parâmetro **Marca**. |
| `<peça>-R<n>` | Recorte (abertura) dentro de uma peça. Ex.: `1234-P2-F1-03-R1`. |
| `<sigla>-<nnn>` | Placa de fábrica de onde a peça é cortada. Ex.: `GST-003` = terceira placa de gesso ST usada no projeto. |

Siglas das placas de fábrica: `CIM` (cimentícia), `GLX` (Glasroc X), `OSB`, `GRU` (gesso RU), `GST` (gesso ST), `GRF` (gesso RF), `GPF` (gesso Performa) e `PRU` (Performa RU).

O campo **Comentários** de cada elemento gerado começa com um prefixo que o plugin usa para saber a quem ele pertence:

| Prefixo | Elemento |
|---|---|
| `LSF \| Parede <Id> \| Painel <n> \| <tipo> \| <perfil>` | estrutura |
| `LSF-PLACA \| ...` | placas (inclui tipo, medida, orientação, área e recortes) |
| `LSF-PARAFUSOS \| ...` | parafusos 3D |

> **Não edite o campo Comentários desses elementos.** É por ele que o plugin apaga e refaz a estrutura de uma parede, lê as placas existentes e monta o quantitativo.

Tipos de elemento de estrutura: Montante principal, Montante de divisa de painel, Montante H (canto em L), Montante Triplo (encontro em T), Montante T (parede passante), Montante Quadruplo (encontro em +), Reforco de vao (King/Jack/verga/contra-verga), Cripple stud, Guia inferior e Guia superior.

---

## 9. Arquivos gravados no seu computador

| Arquivo | Conteúdo |
|---|---|
| `%APPDATA%\LSFRevitPlugin\ultima_selecao.txt` | última escolha da janela de opções de geração, em 4 linhas: sistema (`SteelFrame` ou `Drywall`), largura da alma (mm), bitola (mm) e espaçamento entre montantes (cm). Gravado por `Core\PreferenciasGeracao.cs` |
| `%APPDATA%\LSFRevitPlugin\placas_personalizadas.txt` | medidas de placa que você cadastrou (uma por linha: tipo, lado curto, lado longo) |
| `LSF_Quantitativo_AAAAMMDD_HHMM.csv` | quantitativo gravado por **Gerar CSV**, na pasta do projeto Revit (ou em *Documentos*) |

Os dois arquivos em `%APPDATA%` são por usuário e por máquina, não por projeto do Revit. Se algum estiver ilegível ou não existir, o plugin usa os valores de fábrica (para a janela de opções, o perfil padrão e o espaçamento de 40 cm).

---

## 10. Estrutura do repositório

```
C:\lsf_revit_plugin\
├── LSFRevitPlugin.sln
├── LSFRevitPlugin.addin               Manifesto do Revit
├── rodar-testes.ps1                   Roda os testes com a pasta do Revit no PATH
├── README.md
├── CHECKLIST.md                       Verificações de qualidade e status
├── CLAUDE.md                          Contexto rápido para retomar o projeto
├── DECISOES.md                        Decisões de projeto (ADRs)
├── ROADMAP.md                         Fases e próximos passos
│
├── LSFRevitPlugin\                    Projeto do plugin (net48)
│   ├── LSFRevitPlugin.csproj
│   ├── Core\                          Aplicativo e botões
│   │   ├── App.cs                     Cria a aba "LSF Modeler" e os quatro botões
│   │   ├── Command.cs                 Botão Ativar LSF
│   │   ├── PainelizarCommand.cs       Botão Painelizar paredes
│   │   ├── PlacasCommand.cs           Botão Placas
│   │   ├── QuantitativoCommand.cs     Botão Quantitativo
│   │   ├── PreferenciasGeracao.cs     Última escolha da janela de opções
│   │   └── RevitVersionAdapter.cs     Conversão de unidades centralizada
│   ├── Domain\
│   │   ├── Boards\                    Paginação das placas (sem Revit): catálogo,
│   │   │                              paginador, preparo da entrada, extremo da placa
│   │   ├── Models\                    PerfilSpec, PainelInfo, OpeningInfo, encontros...
│   │   └── Structural\                Nbr14762Calculator (cálculo estrutural puro)
│   ├── Geometry\                      Geometria 3D (DirectShape): montantes, guias,
│   │                                  encontros L/T/+, reforços de vão, materiais
│   ├── Services\                      Geração da estrutura, painelização, placas,
│   │                                  encontros das placas, quantitativo, parafusos
│   └── UI\                            Janelas WPF: opções de geração, ajustar vãos,
│                                      escolha de placas, quantitativo
│
└── Tests\
    └── LSFRevitPlugin.Domain.Tests\   Testes xUnit
```

Princípios do código:

- a lógica de paginação das placas e o cálculo estrutural da NBR 14762 não dependem do Revit (por isso são testáveis fora dele); a paginação trabalha só com medidas em milímetros, em coordenadas do painel (U ao longo da parede, Z a partir do piso);
- tudo que cria geometria usa `DirectShape`;
- toda conversão de unidades passa por `Core\RevitVersionAdapter.cs`;
- a janela de escolha de placas, a do quantitativo e a de parafusos são feitas em código (WPF sem XAML); as de opções de geração e ajuste de vãos usam XAML.

---

## 11. Documentos do projeto

| Arquivo | Para que serve |
|---|---|
| `CHECKLIST.md` | verificações de qualidade (TC-001, TC-002...) com o critério de aceitação e o status de cada uma |
| `ROADMAP.md` | fases do projeto e o que falta fazer |
| `DECISOES.md` | decisões de projeto e o motivo de cada uma |
| `CLAUDE.md` | resumo do ambiente, do mapa de pastas e do estado do projeto, para retomar o trabalho numa nova conversa com um assistente de IA |

---

## 12. Desenvolvimento

Ciclo de trabalho:

1. Feche o Revit.
2. Edite o código.
3. `dotnet build` (na raiz ou em `LSFRevitPlugin\`).
4. `.\rodar-testes.ps1`.
5. Abra o Revit e teste.

Convenções:

- regra que não precisa do Revit vai para `Domain` e ganha teste;
- nomes e mensagens de domínio em português;
- elementos gerados são `DirectShape` etiquetados no campo Comentários (veja a [seção 8](#8-códigos-e-etiquetas));
- gerar estrutura numa parede apaga e refaz tudo o que tem o prefixo daquela parede, dentro de um `TransactionGroup`.

---

## 13. Problemas comuns

| Sintoma | Causa e solução |
|---|---|
| `dotnet build` não consegue gravar a DLL | O Revit está aberto com o plugin carregado. Feche o Revit e compile de novo. |
| Erro de referência a `RevitAPI.dll` ou `RevitAPIUI.dll` | O Revit 2021 não está em `C:\Program Files\Autodesk\Revit 2021\`. Ajuste os `HintPath` (veja a [seção 3](#3-compilar)). |
| A aba **LSF Modeler** não aparece | Confira: (1) o `.addin` está em `%APPDATA%\Autodesk\Revit\Addins\2021\`; (2) a tag `<Assembly>` aponta para uma DLL que existe; (3) você compilou o projeto; (4) se o projeto veio de um `.zip`, os arquivos foram desbloqueados (**Propriedades > Desbloquear**). |
| O Revit pergunta toda vez se pode carregar o plugin | Escolha **Sempre carregar**. A pergunta pode voltar depois de recompilar, porque a DLL não é assinada. |
| Os testes falham ao carregar `RevitAPI` ou acusam falta de DLL nativa | Rode pelo `rodar-testes.ps1` (ele coloca a pasta do Revit no `PATH`) e confira a variável `$revit` dentro dele. |
| `Set-ExecutionPolicy` bloqueia o script de testes | Rode `Set-ExecutionPolicy -Scope Process Bypass` na mesma janela antes do script. |
| "Selecione uma parede reta" | O plugin não suporta paredes curvas. |
| A estrutura sai deslocada | A Linha de Localização da parede não é "Linha de centro da parede". |
| "Nenhuma parede com estrutura gerada foi encontrada" (botão Placas) | Gere a estrutura das paredes com **Ativar LSF** antes. |
| Aviso de que as placas existentes não batem com a paginação | A estrutura, os vãos ou os encontros mudaram depois das placas. Marque **Refazer** no painel. |
| Painelizei, mas a estrutura não mudou | Gere a estrutura da parede de novo com **Ativar LSF** para aplicar os painéis. |
| Aviso de painelização desatualizada | O comprimento da parede mudou depois de painelizar. Use **Painelizar paredes** para redefinir. |
| Peça de placa menor que o mínimo, no relatório | Um vão ou encontro muito perto de uma ponta pode impedir peças maiores. A peça só é aceita abaixo de 400 mm quando uma abertura a corta. |
| Não vejo os parafusos no 3D | Abra o **Quantitativo** e use **Criar parafusos no 3D**. Eles ficam em *Modelo genérico*; confira se essa categoria está visível na vista. A mensagem final diz quantos foram criados e quantos falharam. |
| Os parafusos ficaram desatualizados | Eles não se atualizam sozinhos. Abra o Quantitativo e crie-os de novo. |

---

## 14. Estado do projeto

**Funcionando e validado no Revit 2021**

- geração da estrutura por parede e por painel, com encontros L, T e +, reforços de vão, cripple studs, guias e perfis Drywall e Steel Frame;
- ajuste das medidas dos vãos antes de gerar;
- painelização;
- placas: tipos, medidas, cadastro de medidas, refazer por painel, juntas sobre montantes, recortes, reaproveitamento de sobras, códigos e folga visível de 3 mm;
- quantitativo em janela e em CSV, com a medida dos recortes;
- parafusos da estrutura no quantitativo;
- suíte de **56 testes unitários**, todos passando.

**Em validação**

- largura mínima de 40 cm das peças verticais nas pontas da parede;
- regras de placas nos encontros T e +;
- parafusos das placas e parafusos em 3D (aparecem no modelo; ajustes em andamento).

**Planejado** (detalhes em `ROADMAP.md`)

- desenhos (vistas) com placas e recortes etiquetados e cotados, e marca do código sobre cada placa em corte;
- duas camadas de placas, com juntas desencontradas entre camadas;
- relatório de rejunte por tipo de placa, indicando juntas entre tipos diferentes;
- editar parede já lançada, furação dos perfis e módulo de telhado;
- lançar várias paredes seguidas com o botão **Ativar LSF** ativo;
- evitar junta de placa coincidindo com a abertura vertical de janela e juntas verticais repetidas no mesmo montante;
- código de cada elemento com o painel a que pertence, e seleção de um painel para listar seus componentes;
- validar os coeficientes da NBR 14762 e as regras de fixação de OSB e Glasroc X.

---

## 15. Glossário

| Termo | Significado |
|---|---|
| LSF | *Light Steel Frame*: sistema construtivo com perfis de aço leve |
| Montante | perfil em C vertical da parede |
| Guia | perfil em U horizontal, no piso (inferior) e no topo (superior) |
| Alma, aba (mesa), lábio | partes da seção do perfil: a alma é a face larga; as abas ou mesas são as faces onde as placas são fixadas; o lábio é a dobra na ponta da aba |
| Painel | trecho da parede pré-fabricável, definido em **Painelizar paredes** |
| Fiada | linha horizontal de placas |
| Junta | espaço entre duas placas vizinhas |
| Vão | abertura de porta ou janela |
| King / Jack | montantes que ladeiam um vão (o Jack sustenta a verga; o King vai de guia a guia) |
| Verga / contra-verga | perfis horizontais acima e abaixo do vão |
| Cripple stud | montante curto acima ou abaixo do vão |
| H (Duplo) | reforço do canto em L: par de montantes costas com costas |
| Triplo / Quádruplo | reforços dos encontros em T e em + |
| Costas com costas | dois montantes encostados pelas almas, com as bocas do C para fora |

---

## 16. Referências

- ABNT NBR 14762: dimensionamento de estruturas de aço constituídas por perfis formados a frio (coeficientes ainda a validar contra a norma oficial).
- ABNT NBR 15253: perfis de aço para light steel frame.
- ABNT NBR 15758: sistemas construtivos em chapas de gesso para drywall.
- SINAPI: composições de parede em drywall (espaçamento e distância da borda dos parafusos de gesso).
- Manual da placa cimentícia Brasilit (rev. dez/2025): fixação e juntas.
- Nomenclatura dos encontros (Duplo = L, Triplo = T, Quádruplo = +): baseada no blog da Barbieri.

---

## 17. Licença

Este projeto **ainda não tem licença definida**. Enquanto isso, todos os direitos permanecem reservados ao autor: não copie, modifique nem redistribua o código sem autorização.