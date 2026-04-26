import os
import re

replacements = {
    "Aigueta": "Water",
    "Gosset": "Hot Dog",
    "itemName: Os": "itemName: Bone",
    "Glup, glup, glup! *s'inclina mostrant respecte*": "*Bows respectfully* Glup, glup, glup!",
    "Glup! (Ataca!)": "Glup! (Prepare to be filleted!)",
    "Demanar anecdota": "Ask for a story",
    "Plorar": "Cry",
    "Alagar": "Flatter",
    "Espantar": "Scare",
    "Fer-se el mort": "Play dead",
    "Oferir os": "Offer bone",
    "Insultar": "Insult",
    "Explicar acudit": "Tell a joke",
    "Demanar Disculpes": "Apologize",
    "Accions possibles:": "Available Actions:",
    "Accions disponibles:": "Available Actions:",
    "Li demanes a l'enemic si es que et pot explicar alguna anecdota interessant. ": "You ask the shark for a cool story to break the ice.",
    "Que et penses, que aixo es el bar del poble? Acabare amb tu!": "Do I look like a bartender to you? I'm gonna make you sushi!",
    "Plores suplicant pietat... De sobte, l'enemic se'n adona i reacciona.": "You burst into tears... The shark pauses, caught off guard.",
    "Uaaau! Ets un generador infinit d'aigua?": "Whoa! Are you an infinite water generator? That's kinda rad.",
    "Li dius a l'enemic que el seu estil es molt guai, que t'agrada el seus abdominals i el seu estil futurista. Es posa vermell com un tomaquet.": "You tell the shark his abs are majestic. He blushes furiously.",
    "Glup! Glup! Glup! (Aiii! Para! Que mai m'han dit aquestes coses tan boniques!) ": "Glup! Glup! Glup! (Oh stop it, you! Nobody's ever fluttered my fins like this!)",
    "Mous la teva ma oberta de costat en costat, per intentar allunyar a l'enemic.": "You wave your hands frantically trying to shoo the muscular shark away.",
    "Glup? Glup (Que et penses que soc, una mosca? Soc un tauro amb abdominals).": "Glup? (What do you think I am, a mosquito? I'm a shark with an eight-pack!).",
    "Et tires dramaticament al terra per fingir la teva mort.": "You dramatically flop to the floor, playing dead.",
    "Glup (Ja em conec aquest truc).": "Glup (Nice try, I invented that trick).",
    "Glup... (Suficient...)": "Glup... (Alright, that's enough...)",
    "Li demanes a l'enemic si es que et pot explicar quelcom sobre com ha pogut a arribar a tenir aquest increible fisic. ": "You ask him how he got so incredibly jacked.",
    "Glup! Glup, glup, glup. (Oh! Doncs mira, tot es basa en una bona dieta i esport constant, pero sobretot molta forca de voluntat)": "Glup! (Oh, you know! 100 push-ups, 100 sit-ups, 100 squats, and a strict seafood diet!)",
    "Plores suplicant pietat... L'enemic ni se'n adona ja que esta centrat fent una serie de crunch abdominal.": "You cry for mercy... but the shark is too busy doing a set of tactical crunches to notice.",
    "Glup, glup, glup,... (Nou, deu, onze,...)": "Glup, glup... (nine, ten, eleven...)",
    "Intentes alagar mes a l'enemic, pero te'n adones de que ja s'ho has dit tot abans...": "You try to flatter him more, but realize you've already complimented everything.",
    "Glup? (Alguna cosa a dir?)": "Glup? (Got anything else?)",
    "Mous la teva ma oberta de costat en costat, per intentar allunyar a l'enemic. Es pensa que l'estas intentant ajudar a alleugerir la seva vermellor. ": "You wave your hand horizontally. He thinks you're fanning him off because he's blushing.",
    "Glup! (Gràcies).": "Glup! (Thanks, bro).",
    "Glup?? (Que estas fent?)": "Glup?? (Bruh, what in the ocean are you doing?)",
    "Glup... Glup! (Vaja, sembla que per fi algu li interessa la meva vida!)": "Glup... Glup! (Wow, someone actually cares about my backstory?)",
    "Li demanes a l'enemic si es que et pot explicar alguna anecdota.": "You ask the shark for another story.",
    "Glup... (Ho faria, pero la veritat es que no tinc res mes a explicar)": "Glup... (I'd love to, but I've literally run out of lore.)",
    "Plores per la forta forca de voluntat que ha tingut l'enemic per aconseguir el fisic que molta gent desitjaria.": "You weep out of pure respect for his incredible dedication and gains.",
    "Glup... (Ningu mai s'havia sentit tan orgullos per quelcom que hagi fet jo...)": "Glup... (No one's ever shed a tear over my gym progress before...)",
    "Glup! (Gràcies)": "Glup! (Thanks!)",
    "Glup... Glup! (Aquest descobriment... pot ser or per mi i els meus amics!)": "Glup... Glup! (This discovery... me and the boys are gonna be rich!)",
    "Li demanes a l'enemic si es que et pot explicar alguna anecdota per animar-te, ja que estas trist": "You ask for a story to cheer you up.",
    "Glup! Glup (El que calgui per animar al dispensador d'aigua! Vaig perdre la meva aleta dorsal original en una aposta).": "Glup! (Anything for the water fountain! So I lost my original dorsal fin in a poker game...)",
    "Mentres t'asseques les llagrimes, li dius a l'enemic que admira molt el seu físic.": "Wiping a tear, you tell him his physique is basically god-tier.",
    "Glup... (Uau, aixo no m'ho esperava, moltes gracies...)": "Glup... (Wow... that hit me right in the feels. Thanks, bro.)",
    "Intentes plorar mes, pero ja estas sec...": "You try to cry more, but you are out of tears.",
    "Glup? (Aleshores, no es infinit?)": "Glup? (Wait, so you're not an infinite water glitch?)",
    "Glup? (Que estas fent amb la ma?)": "Glup? (What's with the funny hand waving?)",
    "Glup... (mai ningu s'havia preocupat per coneixer la meva historia...)": "Glup... (Nobody ever cared enough to ask about my past...)",
    "Li demanes a l'enemic si es que et pot explicar com ha aconseguit aquest fisic tan bo": "You ask how he achieved that magnificent swimmer's body.",
    "Glup, glup, glup... (Dieta, esforc i dedicacio)": "Glup, glup... (Blood, sweat, tears, and lots of plankton.)",
    "Alagues la valentia de l'enemic per apostar una part del seu cos (encara que no sigui lo recomenable).": "You praise his bravery for gambling away a body part, even if it wasn't the brightest idea.",
    "Gluup... (Aguantaa...)": "Gluup... (Hold the line...)",
    "Glup?! (Em colpeges i despres em demanes que t'expliqui la meva vida?)": "Glup?! (You smack me and then ask me to be your buddy?)",
    "Alagues la robustesa de l'oponent per soportar el cop que li acabes de fer. No sembla gaire content.": "You compliment his durability for taking your hit. He doesn't look amused.",
    "Glup? (Soc el teu ninot de proves o que?)": "Glup? (What am I, a crash test dummy?)",
    "Plores, per arrepentiment pel que has fet...": "You cry, deeply regretting the violence...",
    "Glup...glup? (T'ha sabut greu... o sigui que potser no ets dolent del tot?)": "Glup... glup? (You feel bad... Maybe you aren't so vile after all?)",
    "Glup? (Creus que aconseguiras quelcom movent la ma d'aquesta manera?)": "Glup? (You think flapping your hand around is gonna save you?)",
    "Glup, glup (Se que ara estas fingint, pero aviat sera veritat)": "Glup! (Keep playing dead, soon you won't have to act!)",
    "Et disculpes per l'agresivitat mostrada fa uns segons.": "You apologize for your sudden outburst of violence.",
    "Glup, glup (D'acord, pero a mi que m'expliques, estem enmig d'una lluita)": "Glup! (Accepted, but save it for outside the ring!)",
    "Sembles una bona persona, crec que m'he equicovat jutjant-te abans de temps... Espero que em perdonis...": "You seem like a chill dude. I might have judged you too quickly... Forgive me?",
    "Ei, que fas que no m'ataques?": "Hey, why aren't you attacking me?",
    "Comences a ballar de forma maldestre el primer que se't ve al cap. L'enemic sembla curios per saber que es el que estas fent.": "You clumsily bust out a random breakdance. The skeleton looks profoundly confused but curious.",
    "Ei! Que fas? Es alguna mena d'atac especial o que?": "Hey! What's that? Some kind of forbidden technique?",
    "Et tires al terra i comences a plorar desconsoladament.. A l'enemic no li sembla importar...": "You fall to the ground and start bawling. The skeleton couldn't care less.",
    "El plor demostra que no ets un guerrer pur de cor... Aixeca't i lluita com ha de ser!": "Tears are a sign of weak fleshy meatbags! Stand up and fight like a warrior!",
    "Li dius a l'enemic que no te res de muscul... L'enemic sembla confos...": "You tell the skeleton his muscle mass is zero. He looks utterly confused.",
    "Que es un muscul?": "What's a 'muscle'?",
    "Alces amb la teva ma l'os que tenies a l'inventari. Abans de que puguis dir res, l'enemic te'l treu de la ma i sembla alegrar-se.": "You hold up a bone. Before you can speak, he snatches it happily.",
    "Es un regal? Per a mi? Es lo millor que em podries haver donat": "A gift? For me?! This is exactly the femur I needed for my collection!",
    "Li dius a l'enemic en forma d'acudit que els esquelets no surten de festa perquè no tenen cos per aguantar-ho. Es posa a riure.": "You tell him my favorite joke: Why don't skeletons go to parties? Because they got NOBODY to go with! He bursts out laughing.",
    "HAHAHAHA, quin bon acudit HAHAHA": "MUAHAHAHA! That's a rib-tickler! Literally!",
    "HAHAHA, em pixaria a sobre... si pogues HAHAHA": "HAHAHA, I'd drop dead if I wasn't already! HAHAHA!",
    "Fas uns passos de flamenco, l'enemic queda sorpres i segueix rient.": "You stomp out some confident flamenco steps. The skeleton claps his bony hands.",
    "UAU, a sobre que ets bon comediant, tambe saps ballar? Quin talent!": "WOW! A stand-up comedian AND a dancer? You're a double threat!",
    "Començes a plorar despres d'haver fet l'acudit. L'enemic s'extranya...": "You start crying out of nowhere right after the joke. The skeleton tilts his skull.",
    "Ei... tot be? Potser no era un acudit i m'he equivocat... Ho sento...": "Uh... you good bro? Was that a tragedy instead of a joke? My bad...",
    "Que es un muscul? No em sona gens ni mica aquesta paraula...": "Seriously, what's a muscle? Sounds like fleshy propaganda.",
    "Li dius en forma d'acudit a l'enemic que el seu menjar preferit son les costelles. No pot parar de riure.": "You joke that his favorite food must be 'spare ribs'. He absolutely loses his mind laughing.",
    "HAHAHAHA, clar, perque les costelles son un os de l'esquelet, HAHAHAHA": "HAHAHAHA! Good one! Because I'm a skeleton! That tickles my funny bone! HAHAHA!",
    "HAHAHAHA, NO PUC PARAR DE RIURE!": "MUAHAHAHA, I CAN'T BREATHE! WAIT, I DONT BREATHE ANYWAY!",
    "Balles una mica per rematar l'assumpte. L'enemic queda impresionat.": "You do a little victory moonwalk. The skeleton is mesmerized.",
    "Ets molt versatil, com un anec! Ho pilles? Perque els anecs saben volar, nadar i caminar! HAHAHA": "You're a jack of all trades! Look at you go, moving without creaking!",
    "Comences a plorar  L'enemic es preocupa per tu...": "You start sobbing uncontrollably. The skeleton steps closer.",
    "Ei... tot bé? Et puc ajudar amb alguna cosa?": "Hey buddy... you okay? Need a tissue? Or... a hug?",
    "Li preguntes a l'enemic que perque els esquelets no van a classe. Abans de poder dir la resposta, ell et talla...": "You ask: 'Why don't skeletons go to school?'. Before you can answer, he interrupts...",
    "Oh! Aquesta me la se, perque no tenen cap cap! HAHAHAHA": "Oh! I know this one! Because their hearts aren't in it! HAHAHAHA!",
    "Que es allò que feies fa un moment?": "What was that weird body wiggling you did earlier?",
    "Balles amb mes confianca. Li dius a l'enemic que el que fas es diu \"ballar\"": "You confidently hit the griddy. You explain that this majestic art is called 'dancing'.",
    "Uaaaau, mai n'havia sentit a parlar! Deixa'm intentar-ho! Ho faig bé?": "Whoooa, never heard of it! Let me try! Am I doing it right?",
    "Comences a plorar despres de ballar. L'enemic esta confos": "You burst into tears mid-dance. The skeleton stands in awkward silence.",
    "Ei... tot be? T'has torçat el tormell quan ballaves?": "Uh... you okay? Did you twist an ankle busting those grooves?",
    "Es un regal? Per a mi? Es lo millor que em podries haver donat!": "For me?! Are you proposing?! Just kidding, thanks!",
    "Que tal, em moc be?": "How's my form? Do I got the moves like Jagger?",
    "Balles. L'enemic et segueix el rotllo, li dons un 10/10. ": "You show off your moves. The skeleton mirrors them perfectly. You rate him a solid 10/10.",
    "Crec que he trobat un nou passatemps preferit! I tot  gracies a tu!": "I think I found my true calling! And it's all thanks to you!",
    "Començes a plorar. L'enemic es preocupa per tu...": "You start to sob hysterically. He looks at you with concern.",
    "Ei... tot be? ": "Yo... everything good at home?",
    "Li preguntes a l'enemic que perque els esquelets no van a classe. Abans de poder dir la resposta, ell respon...": "You ask: 'Why don't skeletons go to school?' Before you can answer, he steals your punchline...",
    "Tranquil Skell, nomes es una esgarrinxada... ": "It's just a scratch, keep it together...",
    "Balles. L'enemic desconfia de les teves accions.": "You inexplicably start dancing. He narrows his eye sockets suspiciously.",
    "Acabare amb tu!": "I'm going to turn you into a pile of dust!",
    "Plores. L'enemic desconfia de les teves accions.": "You cry crocodile tears. He's not buying it.",
    "Li ofereixes un os a mode de disculpes a l'enemic. Sembla que ho accepta.": "You offer a bone as an apology. He begrudgingly accepts it.",
    "Ho agraeixo, encara que fa un moment m'has intentat matar... pero, perdonable...": "Appreciated. I mean, you literally just tried to execute me, but... sure, I forgive you.",
    "Insultes al creador de l'enemic. No sembla afectar-li.": "You roast his creator. He seems completely unfazed.",
    "M'ho esperava venint de tu.": "Didn't expect any less from a chaotic meat machine like you.",
    "Intentes deixar anar l'acudit mes divertit del mon, pero no se t'acudeix res...": "You try to drop the funniest joke ever conceived... but completely draw a blank.",
    "Oh vaja... quina mala sort, un torn de regal per a mi!": "Oh wow... tough crowd! Guess it's my turn to attack!",
    "Demanes perdo pels teus actes agresius. L'enemic ho enten pero tampoc baixa la guardia.": "You repent for your violent behavior. He understands but keeps his guard up.",
    "Entenc.. be, no passa res, pero ara a lluitar!": "I get it... no hard feelings, BUT I'M STILL GONNA CRUSH YOU!"
}

def process_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        changed = False
        for cat, eng in replacements.items():
            if cat in content:
                content = content.replace(cat, eng)
                changed = True

        if changed:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Updated {filepath}")
    except Exception as e:
        pass

for root, dirs, files in os.walk("Assets"):
    if "_Recovery" in root:
        continue
    for file in files:
        if file.endswith((".unity", ".prefab", ".asset", ".cs")):
            process_file(os.path.join(root, file))

print("Completed Asset Replacements.")
