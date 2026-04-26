import os

translations = {
    "No tinguis por en preguntar si tens qualevol dubte!": "Don't be afraid to ask if you have any doubts!",
    "Moltes gracies per la teva compra! No t'arrepentiras, t'ho prometo!": "Thanks for your purchase! I promise you won't regret it!",
    "Moltes gracies pel teu article, m'assegurare de donar-li una bona vida!": "Thanks for selling this to me, I'll take good care of it!",
    "Sembla ser que no tens suficients calers... Torna quan en tinguis mes o": "Looks like you're short on cash... Come back when you have more.",
    "Veig que vas molt carregat, potser seria millor que facis una mica d'espai": "You look too loaded, maybe you should make some space in your inventory.",
    "No aconsegueixo recordar res del que m'ha passat abans d'arribar aqui...": "I can't seem to remember anything that happened before I got here...",
    "Quin cercle tan bonic!": "What a beautiful circle!",
    "'Em recorda a una pilota de ping pong... '": "'Reminds me of a ping pong ball...'",
    "Pero d'un tamany bastant mes gran, la veritat!": "But a lot bigger, honestly!",
    "'Bones... Veig que ets nou per aqui... '": "'Howdy... I see you're new around here...'",
    "Quins modals els meus... Disculpa'm, em dic Ledgermole.": "Where are my manners... Sorry, my name's Ledgermole.",
    "Potser et podria arribar a interessar algun dels meus productes. Soc": "Maybe you'd be interested in some of my wares. I am",
    "Potser et podria arribar a interessar algun dels meus productes. Soc conegut": "Maybe you'd be interested in some of my wares. I'm known",
    "D'acord!": "Alright!",
    "No, gracies...": "No, thanks...",
    "Moltes gracies per la teva visita! A reveure!": "Thanks for your visit! See ya!",
    "No passa res, tranquil, sempre que necessitis quelcom, fes-me una visita.": "No worries, whenever you need something, just drop by.",
    "Hola de nou! T'interessa algun dels meus articles?": "Hello again! Interested in any of my wares?",
    "Deixa'm fer una ullada...": "Let me take a look...",
    "Definitivament no...": "Definitely not...",
    "Moltes gr\\xE0cies per la teva visita, fins aviat! ": "Thanks for your visit, see you soon! ",
    "'No et preocupis, torna quan ho necessitis! '": "'Don't worry, come back when you need!'",
    "Moltes gracies per la teva visita, a reveure!": "Thanks for stopping by, see ya!",
    "No passa res, sempre que necessitis ajuda, estic per aqui, tampoc tinc": "It's fine, whenever you need help, I'm around. It's not like I have",
    "Text d'exemple.": "Example text.",
    "Es un triangle blanc.": "It is a white triangle.",
    '"Perque est\\xE0 inclinat?"': '"Why is it tilted?"',
    "Perque està inclinat?": "Why is it tilted?",
    "Moltes gràcies per la teva visita, fins aviat!": "Thanks for your visit, see you soon!",
    # Extra stuff from C# scripts:
    "Aix\\xED que fas trampes, eh? D'acord, jo far\\xE9 el mateix... Espera... Que": "Oh, so we're using items now? Fine, I'll do the same... Wait... I",
    "    jo no tinc butxaques...": "    don't have any pockets...",
    "Eyy, aix\\xF2 no s'hi val, jo no tinc bra\\xE7os per poder treure'm quelcom de": "Hey, no fair! I don't even have hands to grab items out of my",
    "    l'inventari enmig d'una lluita...": "    pockets in mid-fight...",
    "Mala sort, hahaha!": "Tough luck, hahaha!",
    "Pero a on vas maquina?": "Where do you think you're going, buddy?",
    "Les besties amb rabia mosseguen mes fort!": "Rabid beasts bite way harder!",
    "Awww, m'has fet pupa...": "Ouch, right in the gills...",
    "Recluta a tots els enemics d'aquest tipus per obtenir": "Recruit all enemies of this kind to increase your",
    "    un 20% m\\xE9s de punts de vida.": "    max health by 20%.",
}

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    changed = False
    for cat, eng in translations.items():
        if cat in content:
            content = content.replace(cat, eng)
            changed = True

    if changed:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Updated {filepath}")

for root, dirs, files in os.walk("Assets"):
    if "_Recovery" in root:
        continue
    for file in files:
        if file.endswith((".unity", ".prefab", ".asset", ".cs")):
            process_file(os.path.join(root, file))

print("Translation complete.")
