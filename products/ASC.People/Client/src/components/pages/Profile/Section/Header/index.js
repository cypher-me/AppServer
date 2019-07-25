import React from 'react';
import { Text, IconButton, ContextMenuButton } from 'asc-web-components';
import { withRouter } from "react-router";

const wrapperStyle = {
  display: "flex",
  alignItems: "center"
};

const textStyle = {
  marginLeft: "16px",
  marginRight: "16px"
};

const getUserContextOptions = user => {
  return [
    {
      key: "key1",
      label: "Edit profile",
      onClick: () => console.log("Context action: Edit profile")
    },
    {
      key: "key2",
      label: "Change e-mail",
      onClick: () => console.log("Context action: Change e-mail")
    },
    {
      key: "key3",
      label: "Change phone",
      onClick: () => console.log("Context action: Change phone")
    },
    {
      key: "key4",
      label: "Change password",
      onClick: () => console.log("Context action: Change password")
    },
    {
      key: "key5",
      label: "Disable",
      onClick: () => console.log("Context action: Disable")
    }
  ];
};

const SectionHeaderContent = (props) => {
  const { profile, history } = props;

  return (
    <div style={wrapperStyle}>
      <div style={{ width: '16px' }}>
        <IconButton iconName={'ArrowPathIcon'} color='#A3A9AE' size='16' onClick={history.goBack} />
      </div>
      <Text.ContentHeader truncate={true} style={textStyle}>{profile.displayName}{profile.isLDAP && ' (LDAP)'}</Text.ContentHeader>
      <ContextMenuButton 
        directionX='right' 
        title='Actions' 
        iconName='VerticalDotsIcon' 
        size={16} 
        color='#A3A9AE' 
        getData={getUserContextOptions} 
        isDisabled={false}/>
    </div>
  );
};

export default withRouter(SectionHeaderContent);