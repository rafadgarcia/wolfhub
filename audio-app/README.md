# WolfHub Audio

Aplicativo independente para criar perfis de áudio, escolher endpoints de saída e gravação e alternar entre duas saídas no Windows. A interface é WinForms; a enumeração e o controle de endpoints usam as APIs Core Audio/WASAPI do próprio Windows.

## Compilar e executar

No Windows PowerShell 5.1, a partir da raiz do repositório:

```powershell
.\audio-app\build.ps1
.\audio-app\dist\WolfHubAudio.exe
```

O build usa o compilador do .NET Framework disponível no Windows. Não requer AudioDeviceCmdlets, instalação de módulos PowerShell ou arquivos externos para executar. O executável compilado fica em `audio-app/dist/`, que é ignorado pelo Git.

Para compilar uma cópia de prévia com outro nome:

```powershell
.\audio-app\build.ps1 -OutputName WolfHubAudio-preview.exe
```

## Janela principal e perfis

- A sidebar mostra primeiro o perfil aplicado no Windows. Clique uma vez para visualizar outro perfil; dê duplo clique para ativá-lo.
- O botão **Ativar perfil** aparece quando o perfil aberto não é o perfil aplicado.
- Os cards permanecem empilhados: **Saída principal**, **Saída secundária** e **Dispositivo de gravação**. O card de gravação inclui o medidor de entrada.
- O card principal indica se o endpoint selecionado está ativo no Windows ou apenas salvo no perfil. A gravação e a saída secundária mostram seus estados atuais separadamente.
- Alterar um seletor salva o perfil automaticamente. Se o perfil já estiver aplicado, mudar a saída principal ou o microfone também aplica o endpoint escolhido ao Windows. A saída secundária é salva sem ativação automática.
- A saída secundária é opcional. Se estiver desativada ou indisponível, as ações de alternância ficam desabilitadas.
- **Mais** permite duplicar, renomear ou excluir o perfil. A exclusão pede confirmação e exige que reste pelo menos um perfil.

## Ações de áudio

- **Ativar perfil** define a saída principal e, quando disponível, o microfone desse perfil como padrões do Windows.
- **Usar agora** define o microfone selecionado como padrão de gravação.
- **Alternar** no card secundário, o botão de alternância no rodapé e o duplo clique no ícone da bandeja alternam entre as saídas principal e secundária do perfil aplicado.
- **Testar** reproduz um tom curto diretamente no endpoint escolhido. O teste não muda o padrão do Windows nem redireciona o som para outra saída; erros do endpoint são exibidos no aplicativo.
- O medidor lê o pico dos pacotes WASAPI do microfone enquanto a janela está visível. Os samples são descartados depois do cálculo; o aplicativo não grava nem armazena áudio. Ao ocultar a janela na bandeja, a captura é encerrada.
- Se o Windows bloquear o acesso ao microfone, o medidor informa a permissão necessária e oferece **Abrir permissões**, que abre `ms-settings:privacy-microphone`.

## Configurações globais

A tela **Configurações** substitui a sidebar de perfis enquanto está aberta e reúne ações para todos os endpoints:

- Filtrar a lista por **Todos**, **Saídas** ou **Gravação**.
- Mostrar endpoints desconectados ou desativados quando **Mostrar desconectados / desativados** estiver marcado. Endpoints ocultos continuam listados para que possam ser mostrados novamente.
- Atualizar a enumeração de dispositivos.
- Editar apelidos locais, testar saídas e ocultar/mostrar endpoints nas listas dos perfis.
- Ativar **Iniciar automaticamente com o Windows (na bandeja)**.
- Reiniciar como administrador quando for necessário aplicar a visibilidade também à política de endpoints do Windows.

Apelidos são usados somente no WolfHub Audio; eles não renomeiam o dispositivo globalmente. Ocultar um endpoint o remove das listas dos seletores de perfil e registra essa preferência no XML. Quando o aplicativo já está elevado, ele também tenta aplicar a visibilidade pela API de política do Windows. Sem elevação, a mudança continua valendo dentro das listas do WolfHub. A disponibilidade da política de endpoints depende do Windows e do driver.

## Bandeja do Windows

- Clique simples consulta a saída e o microfone padrões atuais.
- Duplo clique alterna as saídas principal e secundária do perfil aplicado.
- O menu de contexto inclui abrir a janela, selecionar perfis para ativação, consultar o estado atual, alternar saídas, testar a saída padrão e sair.
- Selecionar um perfil no menu **Perfis** aplica a saída principal e o microfone disponível desse perfil.
- Fechar a janela a oculta na bandeja; **Sair** no menu encerra o processo.

## Dados e compatibilidade

As preferências ficam em:

```text
%LOCALAPPDATA%\WolfHubAudio\settings.xml
```

O XML mantém os identificadores reais dos endpoints e contém perfis, apelidos, endpoints ocultos e a preferência de inicialização. Os campos adicionados são opcionais na leitura, mantendo compatibilidade com arquivos anteriores. A execução normal não exige privilégios administrativos. O UAC só é solicitado quando o usuário escolhe **Reiniciar como administrador**.

## Ícones

A interface usa ícones Lucide. Os SVGs-fonte ficam em `audio-app/assets/lucide/svg/`; imagens PNG transparentes são incorporadas ao executável durante o build. A execução não depende desses arquivos externos. `render-icons.ps1` pode regenerar as imagens na máquina de desenvolvimento e usa Chrome instalado. Os assets são licenciados sob ISC; o aviso está em `audio-app/assets/lucide/LICENSE.txt`. O ícone existente do aplicativo continua sendo usado na barra de título, tarefa e bandeja.

## Verificação segura

```powershell
.\audio-app\dist\WolfHubAudio.exe --self-test
```

O autoteste verifica os recursos Lucide, enumeração de endpoints, ordenação/estado de perfis, serialização XML e cálculos/tratamento de erro do medidor. Ele não instala software, não altera os padrões de áudio e não valida fisicamente os alto-falantes ou o microfone. Confirme o som e a entrada em hardware real manualmente.

## Limitações

- A visibilidade gerida sem administrador filtra os endpoints nas listas do WolfHub; para pedir à política do Windows que altere a visibilidade do endpoint, reinicie o app como administrador.
- Apelidos são locais ao aplicativo.
- Não há controles de volume, gravação de áudio ou processamento de microfone.
- O funcionamento das APIs Core Audio e de visibilidade pode variar por dispositivo, driver e versão do Windows.
- O teste de saída é direto ao endpoint e não substitui a validação física do dispositivo.
