namespace Settings

module SettingsDPO =

    let [<Literal>] internal pathDpoWeb1 = @"https://dpo.cz"
    let [<Literal>] internal pathDpoWeb2 = @"https://n.dpo.cz"
    let [<Literal>] internal pathDpoWeb3 = @"https://www.dpo.cz"
    let [<Literal>] internal pathDpoWeb4 = @"https://www.n.dpo.cz"

    let [<Literal>] internal pathDpoWebTimetablesBus = @"/jizdni-rady/jr-bus.html"  

    let [<Literal>] private pathDpoWebTimetablesTrBus = @"/jizdni-rady/jr-trol.html" 
    let [<Literal>] private pathDpoWebTimetablesTram = @"/jizdni-rady/jr-tram.html"

    let internal urlList pathDpoWeb = 
        [
            sprintf "%s%s" pathDpoWeb pathDpoWebTimetablesBus
            sprintf "%s%s" pathDpoWeb pathDpoWebTimetablesTrBus                        
            sprintf "%s%s" pathDpoWeb pathDpoWebTimetablesTram 
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=20%2C21%2C22%2C23%2C24%2C25%2C26%2C27%2C28%2C29"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=30%2C31%2C32%2C33%2C34%2C35%2C36%2C37%2C38%2C39"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=40%2C41%2C42%2C43%2C44%2C45%2C46%2C47%2C48%2C49"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=50%2C51%2C52%2C53%2C54%2C55%2C56%2C57%2C58%2C59"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=60%2C61%2C62%2C63%2C64%2C65%2C66%2C67%2C68%2C69"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=70%2C71%2C72%2C73%2C74%2C75%2C76%2C77%2C78%2C79"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=80%2C81%2C82%2C83%2C84%2C85%2C86%2C87%2C88%2C89"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=90%2C91%2C92%2C93%2C94%2C95%2C96%2C97%2C98%2C99"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND20%2CND21%2CND22%2CND23%2CND24%2CND25%2CND26%2CND27%2CND28%2CND29"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND30%2CND31%2CND32%2CND33%2CND34%2CND35%2CND36%2CND37%2CND38%2CND39"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND40%2CND41%2CND42%2CND43%2CND44%2CND45%2CND46%2CND47%2CND48%2CND49"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND50%2CND51%2CND52%2CND53%2CND54%2CND55%2CND56%2CND57%2CND58%2CND59"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND60%2CND61%2CND62%2CND63%2CND64%2CND65%2CND66%2CND67%2CND68%2CND69"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND70%2CND71%2CND72%2CND73%2CND74%2CND75%2CND76%2CND77%2CND78%2CND79"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND80%2CND81%2CND82%2CND83%2CND84%2CND85%2CND86%2CND87%2CND88%2CND89"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=ND90%2CND91%2CND92%2CND93%2CND94%2CND95%2CND96%2CND97%2CND98%2CND99"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesBus "?dc_filter_80=AE%2CX"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTram "?dc_filter_74=1%2C2%2C3%2C4%2C5%2C6%2C7%2C8%2C9"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTram "?dc_filter_74=10%2C11%2C12%2C13%2C14%2C15%2C16%2C17%2C18%2C19"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTram "?dc_filter_74=ND1%2CND2%2CND3%2CND4%2CND5%2CND6%2CND7%2CND8%2CND9"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTram "?dc_filter_74=ND10%2CND11%2CND12%2CND13%2CND14%2CND15%2CND16%2CND17%2CND18%2CND19"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTrBus "?dc_filter_60=101%2C102%2C103%2C104%2C105%2C106%2C107%2C108%2C109"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTrBus "?dc_filter_60=110%2C111%2C112%2C113%2C114%2C115%2C116%2C117%2C118%2C119"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTrBus "?dc_filter_60=ND101%2CND102%2CND103%2CND104%2CND105%2CND106%2CND107%2CND108%2CND109"
            sprintf "%s%s%s" pathDpoWeb pathDpoWebTimetablesTrBus "?dc_filter_60=ND110%2CND111%2CND112%2CND113%2CND114%2CND115%2CND116%2CND117%2CND118%2CND119"
        ] 
        
        //https://www.dpo.cz/jizdni-rady/jr-bus.html?dc_filter_80=90%2C91%2C92%2C93%2C94%2C95%2C96%2C97%2C98%2C99
        //https://www.dpo.cz/jizdni-rady/jr-tram.html?dc_filter_74=ND1%2CND2%2CND3%2CND4%2CND5%2CND6%2CND7%2CND8%2CND9"