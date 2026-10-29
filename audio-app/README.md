# WolfHub Audio

Aplicativo independente para organizar perfis e alternar endpoints de áudio no Windows. A interface usa WinForms e as APIs Core Audio já presentes no Windows; o build gera um executável sem dependências externas obrigatórias.

## Compilar e executar

No Windows PowerShell 5.1:

```powershell
.\audio-app\build.ps1
.\audio-app\dist\WolfHubAudio.exe
```

Para gerar uma cópia de prévia enquanto outra instância mantém o executável principal aberto:

```powershell
.\audio-app\build.ps1 -OutputName WolfHubAudio-preview.exe
```

O executável é compilado pelo compilador do .NET Framework que acompanha o Windows. Não é um binário NativeAOT. `audio-app/dist/` é ignorado pelo Git.

## Interface e perfis

- Sidebar com perfis, perfil selecionado e indicador separado do perfil aplicado ao Windows.
- Três cards verticais para saída principal, saída secundária e gravação.
- Dispositivos padrão atuais do Windows exibidos separadamente dos valores salvos no perfil.
- Seletores preservam identificadores reais; apelidos são locais ao WolfHub Audio.
- Alterações nos seletores ficam como rascunho até salvar. Trocar ou excluir um perfil com alterações pendentes pede uma decisão.
- Aplicar a saída principal também aplica o microfone configurado. Salvar a saída secundária não a ativa.
- Teste de saída pede confirmação e reproduz o tom diretamente no endpoint escolhido, sem mudar o dispositivo padrão do Windows.
- O card de gravação mostra um medidor instantâneo calculado a partir dos pacotes WASAPI do microfone selecionado. A captura existe somente enquanto a janela está visível; os samples são usados para calcular o pico e descartados, sem gravação ou armazenamento.
- Configurações globais reúnem apelidos, visibilidade de endpoints, inicialização com o Windows e reinício com UAC, sem vínculo com o perfil selecionado.
- A tela de visibilidade lista endpoints Output e Recording, indica o estado reportado, o padrão atual e o uso no perfil; saídas ativas podem ser testadas com confirmação.

## Ícones e licença

A interface usa ícones Lucide oficiais. Os SVGs-fonte ficam em `audio-app/assets/lucide/svg/`; os PNGs transparentes 96×96 são incorporados ao executável durante o build, sem dependência de arquivos externos em tempo de execução. O SVG `play.svg` complementa os arquivos copiados para o projeto. `render-icons.ps1` regenera os PNGs usando Chrome instalado apenas na máquina de desenvolvimento.

Os assets Lucide são licenciados sob ISC; o aviso está em `audio-app/assets/lucide/LICENSE.txt`. O ícone `.ico` do executável, da barra de título e da bandeja continua sendo o recurso existente do aplicativo.

## Bandeja do Windows

- Clique simples consulta saída e microfone atuais.
- Duplo clique alterna principal/secundária do perfil aplicado.
- Botão direito abre a lista de perfis, ações de status, teste, abertura da janela e saída.
- Selecionar um perfil na bandeja aplica a saída principal e o microfone configurado.

## Dados e permissões

Perfis, apelidos, endpoints ocultos pelo WolfHub e preferência de inicialização ficam em `%LOCALAPPDATA%\WolfHubAudio\settings.xml`. Os novos campos são opcionais, preservando o carregamento dos arquivos XML antigos. A execução normal não exige administrador. O UAC só é solicitado quando o usuário escolhe reiniciar elevado para uma ação que possa exigir essa permissão.

Ocultar/mostrar endpoints usa uma interface de política de áudio do Windows e pode variar conforme versão do Windows e driver. O aplicativo não altera o nome global do endpoint, não controla sliders de volume e não instala drivers.

## Teste seguro

```powershell
.\audio-app\dist\WolfHubAudio.exe --self-test
```

O autoteste enumera endpoints e verifica perfis, serialização, cálculo do nível PCM e tradução dos erros de captura em memória. Não instala software, captura áudio real nem altera os padrões de áudio. O teste de tom deve ser feito manualmente para confirmar a saída física em cada endpoint. Se o medidor indicar acesso bloqueado, ele fica desativado; use **Abrir permissões** para abrir a página de microfone do Windows. Ative **Permitir que aplicativos da área de trabalho acessem o microfone** e reabra o WolfHub Audio. Se não houver sinal após isso, confira o mute físico e o nível de entrada do dispositivo no Windows.
