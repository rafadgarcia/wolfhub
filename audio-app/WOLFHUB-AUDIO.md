# WolfHub Audio — Guia de uso e comportamento

## Propósito

O WolfHub Audio é o aplicativo desktop do repositório para organizar perfis de áudio e selecionar dispositivos de reprodução e gravação no Windows. Ele usa WinForms, interfaces Core Audio/WASAPI do Windows e persiste preferências em XML. Não depende do módulo PowerShell `AudioDeviceCmdlets`.

## Perfis: visualizar e ativar

A sidebar diferencia o perfil atualmente visualizado do perfil aplicado aos padrões do Windows. O perfil aplicado aparece primeiro e tem um indicador próprio.

- **Clique simples** em um perfil: abre esse perfil para visualização. Alterações nos seletores são salvas automaticamente.
- **Duplo clique**: abre e ativa o perfil no Windows.
- **Ativar perfil**: aplica a saída principal e o microfone disponíveis do perfil.
- **Mais**: duplicar, renomear ou excluir. Excluir requer confirmação; o último perfil não pode ser removido.

Como as alterações do formulário são salvas automaticamente, a interface não mantém uma etapa de rascunho nem um botão de salvar perfil. O perfil selecionado e o perfil aplicado continuam sendo estados diferentes.

## Dispositivos do perfil

Os cards aparecem em uma única coluna, na ordem atual **Saída principal → Saída secundária → Dispositivo de gravação**. Cada seletor conserva o ID real do endpoint. Apelidos mudam apenas o nome exibido no WolfHub Audio.

### Saída principal

Selecionar outra saída salva o perfil imediatamente. Se o perfil aberto já estiver aplicado, a nova saída também é definida como padrão no Windows. Em um perfil ainda não aplicado, use **Ativar perfil** para aplicar sua saída e gravação.

### Saída secundária

A segunda saída é opcional. Escolher **Nenhuma saída secundária (desativada)** remove a alternativa do perfil. Salvar uma saída secundária não muda o padrão do Windows. **Alternar** só fica habilitado quando as saídas principal e secundária estão disponíveis e são diferentes.

### Dispositivo de gravação

O medidor apresenta o nível de pico lido do endpoint selecionado via WASAPI enquanto a janela está visível. O aplicativo calcula o nível e descarta os samples; não grava nem armazena áudio. Ao ocultar a janela na bandeja, o monitor é parado.

Selecionar um microfone em um perfil já aplicado também o define como padrão no Windows. **Usar agora** define o endpoint escolhido imediatamente. Se o acesso estiver bloqueado, o medidor mostra o estado de permissão e **Abrir permissões** abre as configurações de privacidade do microfone do Windows. Ative a permissão para aplicativos da área de trabalho e reabra o WolfHub Audio.

## Teste das saídas

**Testar** envia um tom curto diretamente ao endpoint de saída selecionado por uma sessão WASAPI. Não muda temporariamente o padrão do Windows. O teste é executado sem uma confirmação intermediária; selecione o dispositivo correto e ajuste o volume do sistema antes de usá-lo. Se o endpoint ou seu formato não for compatível, o erro aparece no aplicativo em vez de o som ser enviado a outro dispositivo.

O teste da lista de Configurações também está disponível apenas para endpoints de saída. O aplicativo não apresenta um teste fictício para microfones.

## Configurações globais

Configurações oculta a sidebar de perfis e oferece uma lista global de dispositivos, com filtros **Todos**, **Saídas** e **Gravação**.

- **Mostrar desconectados / desativados** inclui endpoints inativos na lista. Endpoints ocultos pelo WolfHub permanecem listados para permitir reexibi-los.
- **Atualizar** consulta novamente os endpoints.
- **Editar** permite definir ou limpar o apelido local do endpoint.
- **Ocultar / Mostrar** adiciona ou remove o endpoint das listas de seleção de perfis e atualiza essa preferência imediatamente.
- **Iniciar automaticamente com o Windows (na bandeja)** registra ou remove a entrada de inicialização do usuário atual.
- **Reiniciar como Admin** aparece somente quando o processo não está elevado.

Sem privilégios administrativos, ocultar um dispositivo altera as listas usadas pelo WolfHub Audio. Se o aplicativo já estiver elevado, ele também tenta aplicar a política de visibilidade do endpoint no Windows. Para aplicar essa política, o usuário pode reiniciar o aplicativo como administrador; a inicialização normal permanece sem elevação. A API pode variar conforme Windows e driver.

## Bandeja

- Clique simples: consulta saída e microfone padrões atuais.
- Duplo clique: alterna entre principal e secundária do perfil aplicado.
- Menu de contexto: abrir a janela, ativar um perfil, consultar estado, alternar saídas, testar a saída padrão ou sair.
- Fechar a janela mantém o aplicativo na bandeja; **Sair** encerra o processo.

## Persistência

O arquivo usado é `%LOCALAPPDATA%\WolfHubAudio\settings.xml`. Ele guarda os IDs dos endpoints, perfis, apelidos, endpoints ocultos e preferência de inicialização. Os campos adicionais são opcionais para que arquivos XML de versões anteriores continuem carregando.

## Build e autoteste

Na raiz do projeto, em Windows PowerShell 5.1:

```powershell
.\audio-app\build.ps1
.\audio-app\dist\WolfHubAudio.exe --self-test
```

O autoteste enumera endpoints e verifica recursos de ícones, estado de perfil, serialização e lógica de cálculo/tratamento do medidor. Ele não instala aplicativos, muda padrões do Windows, testa o som físico nem confirma uma entrada real de microfone.

## Limitações conhecidas

- Apelidos não renomeiam endpoints globalmente no Windows.
- Não há controle de volume, gravação de áudio ou processamento de microfone.
- A aplicação global de visibilidade pode depender de elevação e suporte do driver; a filtragem local nas listas do WolfHub permanece disponível.
- Testes de áudio físico, permissões do microfone e escalas DPI precisam ser confirmados no computador de destino.
