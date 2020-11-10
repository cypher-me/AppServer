import React from "react";
import styled from "styled-components";
import RectangleLoader from "../RectangleLoader/index";

const StyledContainer = styled.div`
  margin-top: 13px;
  margin-bottom: 10px;
`;

const ArticleHeaderLoader = (props) => {
  return (
    <StyledContainer>
      <RectangleLoader {...props} />
    </StyledContainer>
  );
};

export default ArticleHeaderLoader;