[직접 TCP 홀펀칭을 구현](https://github.com/yjd6808/_Fork_tcp-holepunching) 해본후 내가 본 답지 코드이다.  

[수정 내역]
 - "P2P 연결"만 구현되어져 있어서 P2P 통신 여부를 확인하기 위한 코드를 추가해줬다.
 - 가상머신에 배포하기 위해서 닷넷 CLI로 빌드를 할 때 오류가 발생해서 
   타겟 프레임워크를 .Net Framework 4.0에서 .Net 6.0으로 바꿔줬다.
    