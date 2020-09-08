import React, { useEffect } from  'react';
import styled from 'styled-components';
import { connect } from 'react-redux';
import { 
  Heading,
  ToggleButton 
} from 'asc-web-components';

import { 
  setUpdateIfExist, 
  setStoreOriginal, 
  setEnableThirdParty,
  setConfirmDelete, 
  setStoreForceSave,
  setSelectedNode,
  setForceSave
} from '../../../../../store/files/actions';

const StyledSettings = styled.div`
  display: grid;
  grid-gap: 10px;

  .toggle-btn {
    position: relative;
  }

  .heading {
    margin-bottom: 0;
    margin-top: 22px;
  }
`;

function SectionBodyContent(props) {
  useEffect(() => {
    const { setting, t } = props;
    document.title = t(`${setting}`);
    return function setTitle() {
      document.title = 'ASC.Files';
    }
  }, [props.setting])

  useEffect(() => {
    const { setting, selectedTreeNode, setSelectedNode } = props;
    if( setting !== selectedTreeNode[0] ) {
      setSelectedNode([ setting ])
    }
  }, [props.setting]);

  const onChangeStoreForceSave = () => {
    const { storeForceSave, setStoreForceSave } = props;
    setStoreForceSave( !storeForceSave, "storeForceSave" );
  }

  const onChangeThirdParty = () => {
    const { enableThirdParty, setEnableThirdParty } = props;
    setEnableThirdParty(!enableThirdParty, "enableThirdParty");
  }

  const renderAdminSettings = () => {

    const {
      enableThirdParty,
      storeForceSave,
      t
    } = props;

    return (
      <StyledSettings>
        <ToggleButton 
          className="toggle-btn"
          label={t('intermediateVersion')}
          onChange={onChangeStoreForceSave}
          isChecked={storeForceSave}
        />
        <ToggleButton
          className="toggle-btn"
          label={t('thirdPartyBtn')}
          onChange={onChangeThirdParty}
          isChecked={enableThirdParty}
        />
      </StyledSettings>
    )
  }

  const onChangeOriginalCopy = () => {
    const { storeOriginalFiles, setStoreOriginal } = props;
    setStoreOriginal( !storeOriginalFiles, "storeOriginalFiles" );
  }

  const onChangeDeleteConfirm = () => {
    const { confirmDelete, setConfirmDelete } = props;
    setConfirmDelete( !confirmDelete, "confirmDelete" );
  }

  const onChangeUpdateIfExist = () => {
    const { updateIfExist, setUpdateIfExist } = props;
    setUpdateIfExist( !updateIfExist, "updateIfExist" );
  }

  const onChangeForceSave = () => {
    const { forceSave, setForceSave } = props;
    setForceSave( !forceSave, "forceSave" );
  }

  const renderCommonSettings = () => {
    const {
      updateIfExist,
      confirmDelete,
      storeOriginalFiles,
      forceSave,
      t
    } = props;

    return (
      <StyledSettings>
        <ToggleButton
          className="toggle-btn"
          label={t('originalCopy')}
          onChange={onChangeOriginalCopy}
          isChecked={storeOriginalFiles}
        />
        <ToggleButton
          className="toggle-btn"
          label={t('displayNotification')}
          onChange={onChangeDeleteConfirm}
          isChecked={confirmDelete}
        />
        <ToggleButton
          isDisabled={true}
          className="toggle-btn"
          label={t('displayRecent')}
          onChange={(e)=>console.log(e)}
          isChecked={false}
        />
        <ToggleButton
          isDisabled={true}
          className="toggle-btn"
          label={t('displayFavorites')}
          onChange={(e)=>console.log(e)}
          isChecked={false}
        />
        <ToggleButton
          isDisabled={true}
          className="toggle-btn"
          label={t('displayTemplates')}
          onChange={(e)=>console.log(e)}
          isChecked={false}
        />
        <Heading className="heading" level={2} size="small">{t('storingFileVersion')}</Heading>
        <ToggleButton
          className="toggle-btn"
          label={t('updateOrCreate')}
          onChange={onChangeUpdateIfExist}
          isChecked={updateIfExist}
        />
        <ToggleButton
          className="toggle-btn"
          label={t('keepIntermediateVersion')}
          onChange={onChangeForceSave}
          isChecked={forceSave}
        />
      </StyledSettings>
    );
  }

  const renderClouds = () => {
    return (<></>)
  }

  const { setting, enableThirdParty, isAdmin } = props;
  let content;
  if(setting === 'admin' && isAdmin)
    content = renderAdminSettings();
  if(setting === 'common') 
    content = renderCommonSettings();
  if(setting === 'thirdParty' && enableThirdParty )
    content = renderClouds();
  return content; 
}

function mapStateToProps(state) {
  const { settingsTree, selectedTreeNode } = state.files;
  const { isAdmin } = state.auth.user;
  const { 
    storeOriginalFiles,
    confirmDelete,
    updateIfExist,
    forceSave,
    storeForceSave,
    enableThirdParty
  } = settingsTree;

  return { 
    isAdmin,
    selectedTreeNode,
    storeOriginalFiles,
    confirmDelete,
    updateIfExist,
    forceSave,
    storeForceSave,
    enableThirdParty
  }
}

export default connect(
  mapStateToProps, { 
    setUpdateIfExist, 
    setStoreOriginal, 
    setEnableThirdParty,
    setConfirmDelete,
    setStoreForceSave,
    setSelectedNode,
    setForceSave
  })(SectionBodyContent);